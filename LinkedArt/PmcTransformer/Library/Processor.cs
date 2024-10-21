using LinkedArtNet.Parsers;
using LinkedArtNet.Vocabulary;
using LinkedArtNet;
using PmcTransformer.Helpers;
using System.Xml.Linq;
using System.Text.Json;
using Group = LinkedArtNet.Group;
using System.Data;
using PmcTransformer.Reconciliation;
using Microsoft.Extensions.Primitives;

namespace PmcTransformer.Library
{
    public class Processor
    {
        private readonly Reconciler reconciler;
        private static readonly char[] separator = [',', ' '];

        public Processor(
            Reconciler reconciler
        )
        {
            this.reconciler = reconciler;
        }

        public async Task ProcessLibrary(XDocument xLibrary, bool assignAndWrite = false)
        {
            var timespanParser = new TimespanParser();

            // Maps
            var allWorks = new Dictionary<string, LinguisticObject>();
            var allHMOs = new Dictionary<string, List<HumanMadeObject>>();

            // For reconciliation
            var persAuthorFullDict = new Dictionary<string, ParsedAgent>();
            var corpAuthorDict = new Dictionary<string, ParsedAgent>();
            var publisherDict = new Dictionary<string, ParsedAgent>(); ;
            var keywordDict = new Dictionary<string, List<string>>();
            var placeDict = new Dictionary<string, List<string>>();

            int nullMediumCounter = 0;
            var classCounter = new Dictionary<int, int>();

            var distinctClasses = new HashSet<string>();
            var accLocCounter = new Dictionary<int, int>();
            int classMatchesAccLocCounter = 0;
            int classHasLocationButDifferentFromAcclocCounter = 0;
            var collationCounter = new Dictionary<int, int>();
            var distinctLang = new HashSet<string>();
            var langCounter = new Dictionary<int, int>();
            int accessionMismatch = 0;


            var conn = DbCon.Get();
            var cleanedKeywordDict = LoadCleanedKeywords();

            foreach (var record in xLibrary.Root!.Elements())
            {
                // Each of these is a work - a LinguisticObject
                // These carry the semantic metadata, the Creation activities.
                // Each work has one or more HumanMadeObjects, which carry the physical metadata.

                if (Helpers.ShouldSkipRecord(record))
                {
                    continue;
                }

                // /identified_by[type=Identifier,classified_as=REPOSITORY]/value
                var id = record.Attribute("ID")!.Value;

                // /identified_by[type = Name, classified_as = PRIMARY] / value
                var title = record.LibStrings("title").Single();

                // The first iteration will focus only on the books. 
                var work = new LinguisticObject()
                    .WithContext()
                    .WithId($"{Identity.LibraryLinguistic}{id}")
                    .WithLabel(title);
                allWorks.Add(id, work);

                work.IdentifiedBy = [
                    new Identifier(id).AsSystemAssignedNumber(),
                    new Name(title).AsPrimaryName(),
                ];

                work.ReferredToBy ??= [];
                work.ReferredToBy.Add(
                    new LinguisticObject()
                        .WithClassifiedAs(Getty.AccessStatement, Getty.BriefText)
                        .WithContent(Statements.AccessStatement)
                );

                // Now create 1 or more HumanMadeObjects for the Work,
                // based on the parallel accloc (location) and accnofld (accession number) fields.
                var acclocs = record.LibStrings("accloc").ToList();
                var accessionNumbers = record.LibStrings("accnofld").ToList();
                accLocCounter.IncrementCounter(acclocs.Count);

                if (accessionNumbers.Count > 0 && acclocs.Count != accessionNumbers.Count)
                {
                    throw new InvalidOperationException("Mismatch accloc/accnofld for " + id);
                }

                List<string>? hmoIdParts = null;

                if (acclocs.Count > 0)
                {
                    // more than one HMO
                    if(acclocs.Count == accessionNumbers.Count && accessionNumbers.Distinct().Count() == accessionNumbers.Count)
                    {
                        // use accession numbers as the HMO part of the ID
                        hmoIdParts = accessionNumbers.Select(s => "accno_" + s).ToList();
                    }
                    else
                    {
                        // we can't use accessionNumbers as IDs for HMOs
                        if (acclocs.Distinct().Count() == acclocs.Count)
                        {
                            // ...but we can use acclocs as IDs
                            hmoIdParts = acclocs.Select(s => "accloc_" + s).ToList();
                        }
                    }
                    if(hmoIdParts == null)
                    {
                        int partCounter = 0;
                        hmoIdParts = acclocs.Select(s => $"{++partCounter}").ToList();
                    }
                }

                var alreadyMappedLocations = new HashSet<Place>();
                allHMOs[id] = [];
                if (acclocs.Count == 0)
                {
                    var hmo = new HumanMadeObject()
                        .WithContext()
                        .WithId($"{Identity.LibraryHmo}{id}/1");
                    hmo.IdentifiedBy = [new Name(title).AsPrimaryName()];
                    allHMOs[id].Add(hmo);
                }
                else
                {
                    for (int i = 0; i < acclocs.Count; i++)
                    {
                        var hmo = new HumanMadeObject()
                            .WithContext()
                            .WithId($"{Identity.LibraryHmo}{id}/{hmoIdParts![i]}"); // was -{i + 1}
                        hmo.IdentifiedBy = [
                            new Name(title).AsPrimaryName()
                        ];
                        if (accessionNumbers.Count > 0)
                        {
                            hmo.IdentifiedBy.Add(
                                new Identifier(accessionNumbers[i]).AsAccessionNumber());
                        }
                        var mappedLocation = Locations.FromRecordValue(acclocs[i]);
                        if (mappedLocation != null)
                        {
                            alreadyMappedLocations.Add(mappedLocation);
                            hmo.CurrentLocation = mappedLocation;
                        }
                        allHMOs[id].Add(hmo);
                    }
                }
                foreach (var hmo in allHMOs[id])
                {
                    // add relationship from HMO to LO
                    hmo.Carries = [
                        new LinguisticObject().WithId(work.Id)
                    ];
                    // say it is part of the library set
                    hmo.MemberOf = [
                        Locations.PMCLibrarySet
                    ];
                    hmo.ReferredToBy ??= [];
                    hmo.ReferredToBy.Add(
                        new LinguisticObject()
                            .WithClassifiedAs(Getty.AccessStatement, Getty.BriefText)
                            .WithContent(Statements.AccessStatement)
                    );
                }


                // Now augment from the <notes> field. 
                // Gather notes that may supplement (or supplant) later field values 
                var hierarchicalPlaces = new List<string>();
                var editionStatementsFromNotes = new List<string>();
                var accessionNumbersFromNotes = new List<string>();
                NotesField.ParseNotesField(
                    record, work, allHMOs[id],  // !!! <<<< We haven't populated this yet!
                    hierarchicalPlaces, editionStatementsFromNotes, accessionNumbersFromNotes
                );
                if (accessionNumbersFromNotes.Count == 1)
                {
                    var accParts = accessionNumbersFromNotes[0].Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(x => x.Trim())
                        .Where(x => x != "X")
                        .Where(x => x != "All")
                        .Where(x => x != "PMF")
                        .Where(x => x != "(PMF)")
                        .Where(x => x != "Archive")
                        .Where(x => x != "(Missing)")
                        .ToList();
                    // The problem here is that the accession number belongs to the HMO, but from the notes
                    // field we don't know _which_ HMO. But if there is only one we're OK.
                    if (allHMOs[id].Count == 1)
                    {
                        foreach (var accessionNumber in accessionNumbersFromNotes)
                        {
                            allHMOs[id][0].IdentifiedBy!.Add(
                                new Identifier(accessionNumber).AsAccessionNumber());
                        }
                    }
                    else
                    {
                        if (accParts.Count != 0)
                        {
                            if (accParts.Count == allHMOs[id].Count)
                            {
                                for (var i = 0; i < accParts.Count; i++)
                                {
                                    allHMOs[id][i].IdentifiedBy!.Add(new Identifier(accParts[i]).AsAccessionNumber());
                                }
                            }
                            else if (accParts.Count == 1)
                            {
                                // !! Danger.. this may not be valid, typically records have different acclocs,
                                // so do they all have the same accession number? Probably not.
                                foreach (var hmo in allHMOs[id])
                                {
                                    hmo.IdentifiedBy!.Add(new Identifier(accParts[0]).AsAccessionNumber());
                                }
                            }
                            else
                            {
                                // ?? We have more than one HMO, and one (or more) accession numbers from notes fields
                                // Just give ALL the HMOs the accession number.
                                Console.WriteLine($"{++accessionMismatch} (ACN) for {id}: {allHMOs[id].Count} HMOs, and notes is {string.Join("||(ACN)", accessionNumbersFromNotes)}");
                            }
                        }
                    }
                }
                if (accessionNumbersFromNotes.Count > 1)
                {
                    Console.WriteLine($"{++accessionMismatch} MULTIPLE (ACN) for {id}: {allHMOs[id].Count} HMOs, and notes is {string.Join("||(ACN)", accessionNumbersFromNotes)}");
                }
                // NB 52 records that log messages above, mostly the first message. 466 if you don't do the "Danger" one.

                Helpers.AddEdition(record, work, editionStatementsFromNotes);

                bool missingMedium = Helpers.AddMedium(record, work);
                if (missingMedium) nullMediumCounter++;

                var classes = Helpers.GetClasses(record);
                classCounter.IncrementCounter(classes.Count);
                string[] normalisedMediums = [
                    "PAMPHLET",
                    "LARGE",
                    "EXTRA LARGE",
                    "EXTRA EXTRA LARGE",
                    "INFORMATION FILES",
                    "REPORTS"
                ];

                var identifierClasses = new List<string>();
                string? normalisedMedium = null;

                foreach (var classVal in classes)
                {
                    var locationAsClass = Locations.FromRecordValue(classVal, true);
                    if (locationAsClass != null)
                    {
                        if (alreadyMappedLocations.Contains(locationAsClass))
                        {
                            classMatchesAccLocCounter++;
                        }
                    }
                    else
                    {
                        
                        var mediumClass = classVal.ToUpperInvariant().Replace("(", "").Replace(")", "");
                        if(normalisedMedium != null)
                        {
                            throw new NotSupportedException("no");
                        }
                        normalisedMedium = normalisedMediums.FirstOrDefault(m => m.StartsWith(mediumClass));
                        if (normalisedMedium != null)
                        {
                            switch (normalisedMedium)
                            {
                                case "PAMPHLET":
                                    work.WithClassifiedAs(Media.FromRecordValue("Pamphlet")!);
                                    break;
                                case "LARGE":
                                case "EXTRA LARGE":
                                case "EXTRA EXTRA LARGE":
                                    work.WithClassifiedAs(Media.FromRecordValue("Large")!);
                                    break;
                                case "INFORMATION FILES":
                                    work.WithClassifiedAs(Media.FromRecordValue("Information files")!);
                                    break;
                                default:
                                    throw new InvalidOperationException("Unexpected medium");
                            }
                        }
                        else
                        {
                            identifierClasses.Add(classVal);
                            // This class value is an Identifier
                        }
                    }

                }

                foreach(var classVal in identifierClasses)
                {
                    var identifier = classVal;
                    if (normalisedMedium.HasText())
                    {
                        identifier = classVal + " (" + normalisedMedium + ")";
                    }
                    work.IdentifiedBy.Add(new Identifier(classVal)
                        .WithClassifiedAs(Getty.AatType("Class", "300435444")));
                }


                // =====================
                // Entities to be reconciled - but may become assigned to different categories
                // =====================

                // Not all actors become linked Persons, because:

                //  former owner, donor, types just to Provenance Statement
                //  publisher starting "Printed for" should be a Publication Statement instead of a group
                //  if printed for and sold by in publisher, then make it a provenance statement
                //  persauthorfull / corpauthor with a role of Publisher is better than < publisher > as less extraneous text
                //  addressee becomes a subject not an author
                //  former owner, donor on HMO
                //  binder and printer are Production on HMO

                var publishersInOtherFields = new List<ParsedAgent>();
                // %% need to pull out special handling

                // Repeatable. Personal name of a creator/contributor to the Work, plus the role they played. See notes on People below.
                // /created_by/part/carried_out_by/id (for person)
                // /created_by/part/classified_as/id (for role)
                var persAuthors = record.LibStrings("persauthorfull");
                foreach (var authorString in persAuthors)
                {
                    // NOW LOOK AT ROLES
                    var author = new ParsedAgent(authorString);
                    var role = author.Role?.ToLowerInvariant();
                    if (role == "former owner" || role == "donor")
                    {
                        foreach (var hmo in allHMOs[id])
                        {
                            hmo.ReferredToBy ??= [];
                            hmo.ReferredToBy.Add(
                                new LinguisticObject()
                                    .WithClassifiedAs(Getty.ProvenanceActivity, Getty.BriefText)
                                    .WithContent(authorString));
                        }
                    }
                    else if (role == "addressee")
                    {
                        keywordDict.AddToListForKey(authorString.TrimOuterBrackets(), id);
                    }
                    else if (role == "publisher")
                    {
                        publishersInOtherFields.Add(author);
                    }
                    else
                    {
                        persAuthorFullDict.AddToListForKey(authorString, id, author);
                    }
                }

                // corpauthor - Repeatable. Organization/Corporate name of a creator/contributor to the Work.
                // No role provided, assume role=author.
                // See notes on Groups below.
                // /created_by/part/carried_out_by/id
                var corpAuthors = record.LibStrings("corpauthor");
                foreach (var authorString in corpAuthors)
                {
                    var author = new ParsedAgent(authorString);
                    var role = author.Role?.ToLowerInvariant();
                    if (role == "publisher")
                    {
                        publishersInOtherFields.Add(author);
                    }
                    else
                    {
                        corpAuthorDict.AddToListForKey(authorString, id, author);
                    }
                }

                // See example output E2865, D8326
                // /about/id
                var keywords = record.LibStrings("keywords");
                foreach (var keyword in keywords)
                {
                    string? tidiedKeyword;
                    if(!cleanedKeywordDict.TryGetValue(keyword, out tidiedKeyword))
                    {
                        Console.WriteLine("Going to DB for " + keyword);
                        tidiedKeyword = conn.GetCleanedSubject(id, keyword)?.KeywordsCleaned ?? keyword;
                        cleanedKeywordDict[keyword] = tidiedKeyword;
                    }
                    keywordDict.AddToListForKey(tidiedKeyword, id);
                }



                var places = record.LibStrings("place").Select(p => p.TrimOuterBrackets()).ToList();
                // place
                bool hasPlace = hierarchicalPlaces.Count > 0;
                // /used_for[classified_as=PUBLISHING]/took_place_at/id
                if (hasPlace)
                {
                    foreach (var place in hierarchicalPlaces)
                    {
                        placeDict.AddToListForKey(place.TrimOuterBrackets(), id);
                    }
                }
                else
                {
                    // Only use place field if we didn't find a hierarchical place in notes->(HIE)
                    foreach (var place in places)
                    {
                        hasPlace = true;
                        placeDict.AddToListForKey(place, id);
                    }
                }
                // #1 we have multiple places here


                // publisher
                // /used_for[classified_as=PUBLISHING]/carried_out_by/id

                LinkedArtObject? publicationStatement = null;

                var publishers = record.LibStrings("publisher").ToList();
                bool hasPublisher = false;
                if (publishersInOtherFields.Count > 0)
                {
                    // persauthorfull / corpauthor with a role of Publisher is better than <publisher> as less extraneous text
                    hasPublisher = true;
                    foreach (var publisher in publishersInOtherFields)
                    {
                        publisherDict.AddToListForKey(publisher.Original, id, publisher);
                    }
                }
                else
                {
                    foreach (var publisher in publishers)
                    {
                        var pubLower = publisher.ToLowerInvariant();
                        var pubTrimmed = publisher.TrimOuterBrackets();
                        if (string.IsNullOrWhiteSpace(pubTrimmed))
                        {
                            continue;
                        }
                        if (
                            (pubLower.IndexOf("published") != -1 && pubLower.IndexOf("sold by") != -1)
                            ||
                            pubLower.StartsWith("printed by")
                        )
                        {
                            publicationStatement = new LinguisticObject()
                                    .WithClassifiedAs(Getty.PublicationStatement, Getty.BriefText)
                                    .WithContent(pubTrimmed);

                        }
                        else if (pubLower.IndexOf("printed for") != -1 || pubLower.IndexOf("sold by") != -1)
                        {
                            foreach (var hmo in allHMOs[id])
                            {
                                hmo.ReferredToBy ??= [];
                                hmo.ReferredToBy.Add(
                                    new LinguisticObject()
                                        .WithClassifiedAs(Getty.ProvenanceActivity, Getty.BriefText)
                                        .WithContent(pubTrimmed));
                            }
                        }
                        else
                        {
                            hasPublisher = true;
                            publisherDict.AddToListForKey(publisher, id);
                        }
                    }

                }

                var year = record.LibStrings("year").SingleOrDefault();
                var nobrackets = year.TrimOuterBrackets();

                var firstPublicationStatement = "";
                if(places.Count > 0)
                {
                    firstPublicationStatement += places[0] + ": ";
                }
                if(publishers.Count > 0)
                {
                    firstPublicationStatement += publishers[0];
                }
                if(firstPublicationStatement.HasText())
                {
                    if(nobrackets.HasText())
                    {
                        firstPublicationStatement += $": [{nobrackets}]";
                    }
                }
                if(firstPublicationStatement.HasText())
                {
                    work.ReferredToBy ??= [];
                    work.ReferredToBy.Add(
                        new LinguisticObject()
                            .WithClassifiedAs(Getty.PublicationStatement, Getty.BriefText)
                            .WithContent(firstPublicationStatement));
                }

                if(publicationStatement != null)
                {
                    work.ReferredToBy ??= [];
                    work.ReferredToBy.Add(publicationStatement);
                }

                if (nobrackets != null && nobrackets.StartsWith("n.d")) year = null;
                if (nobrackets == "Date of publication not identified") year = null;

                var timespan = timespanParser.Parse(year);

                if (year != null || hasPublisher || hasPlace)
                {
                    if (year != null && !char.IsDigit(year[0]))
                    {
                        //Console.WriteLine(year);
                    }
                    // create a publishing activity which we can populate with reconciled place and publisher later
                    work.UsedFor = [
                        new Activity()
                        {
                            Label = "Publishing",
                            ClassifiedAs = [Getty.Publishing],
                            TimeSpan = timespan, // which may be null
                            CarriedOutBy = null, // [ group.. ]
                            TookPlaceAt = null // [ place.. ]
                        }
                    ];
                }

                Helpers.AddCollation(record, work);


                // entrydate
                // TODO: use in Activity Stream - in DB


                Helpers.AddSeriesStatement(record, work);

                int langCount = Helpers.AddLanguage(distinctLang, record, work);
                langCounter.IncrementCounter(langCount);

                Helpers.AddAccessStatement(record, work);



            }

            // #######################################################################################
            // ####################### Finished first pass through all records #######################
            // #######################################################################################

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();

            SaveCleanedKeywords(cleanedKeywordDict!);


            // For reconciliation:
            //    persAuthorFullDict
            //    corpAuthorDict
            //    publisherDict
            //    keywordDict
            //    placeDict

            Console.WriteLine("nullMediumCounter: " + nullMediumCounter);
            Console.WriteLine("allBooks keys: " + allWorks.Keys.Count);
            Console.WriteLine();

            Console.WriteLine("persauthorfull keys: " + persAuthorFullDict.Keys.Count);
            Console.WriteLine("corpAuthor keys: " + corpAuthorDict.Keys.Count);
            Console.WriteLine("publisher keys: " + publisherDict.Keys.Count);
            Console.WriteLine("keyword keys: " + keywordDict.Keys.Count);
            Console.WriteLine("place keys: " + placeDict.Keys.Count);
            Console.WriteLine();

            classCounter.Display("Distribution of class values:");
            accLocCounter.Display("Distribution of accloc values:");

            Console.WriteLine("Temporarily mapped UO, QUA, DUP");
            Locations.ShowUnmapped();

            // What to do...
            // multipleAccLocCounter.Display("Books with more than one distinct accloc:");


            Console.WriteLine("classMatchesAccLocCounter: " + classMatchesAccLocCounter);
            Console.WriteLine("classHasLocationButDifferentFromAcclocCounter: " + classHasLocationButDifferentFromAcclocCounter);


            //  accnofldCounter.Display("Distribution of accessionNumbers:");
            collationCounter.Display("Distribution of collations:");
            langCounter.Display("Distribution of languages:");

            Console.WriteLine("-------------------");
            foreach (var c in distinctLang)
            {
                Console.WriteLine(c);
            }


            // agents
            // await reconciler.Reconcile(allWorks, corpAuthorDict, "corpauthor", "Group", true);
            // await reconciler.Reconcile(allWorks, publisherDict, "publisher", "Group", true);
            // await reconciler.Reconcile(allWorks, persAuthorFullDict, "persauthorfull", "Person", true);

            // other authorities
            await reconciler.Reconcile(allWorks, keywordDict, "keywords", "Concept", false);
            await reconciler.Reconcile(allWorks, placeDict, "place", "Place", false);


            // To be done later and rewritten!

            if (assignAndWrite)
            {
                AssignCorpAuthors(allWorks, corpAuthorDict);
                AssignPersAuthors(allWorks, allHMOs, persAuthorFullDict);
                AssignPlaces(allWorks, placeDict);
                AssignPublishers(allWorks, publisherDict);
                AssignSubjects(allWorks, keywordDict);

                foreach (var work in allWorks)
                {
                    Writer.WriteToDisk(work.Value);
                    foreach (var hmo in allHMOs[work.Key])
                    {
                        Writer.WriteToDisk(hmo);
                    }
                }
            }
        }

        private Dictionary<string, string>? LoadCleanedKeywords()
        {
            try
            {
                var jsonString = File.ReadAllText("C:\\pmc\\ser\\cleanedKeywords.json");
                return JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString);
            }
            catch
            {
                return [];
            }
        }

        private void SaveCleanedKeywords(Dictionary<string, string> cleanedKeywords)
        {
            string jsonString = JsonSerializer.Serialize(cleanedKeywords);
            File.WriteAllText("C:\\pmc\\ser\\cleanedKeywords.json", jsonString);
        }

        private static void AssignSubjects(Dictionary<string, LinguisticObject> allWorks, Dictionary<string, List<string>> keywordDict)
        {
            var conn = DbCon.Get();

            foreach (var keyword in keywordDict)
            {
                var keywordString = keyword.Key;
                LinkedArtObject? subjectRef = null;
                LinkedArtObject? full = null;

                var authority = conn.GetAuthorityFromSourceString("keywords", keywordString, true);
                if (authority == null)
                {
                    throw new InvalidOperationException("Must have an Authority at this point");
                }
                if (authority.Unreconciled)
                {
                    // Here we don't know what Type this should be
                    // - it could be a Person, a Material, a Place...
                    authority.Type = "Type";
                    authority.Label = keywordString;
                }
                subjectRef = authority.GetReference();
                full = authority.GetFull();

                if (subjectRef == null || full == null)
                {
                    throw new InvalidOperationException("Must have a LinkedArtObject at this point");
                }
                Writer.WriteToDisk(full);


                // /used_for[classified_as=PUBLISHING]/carried_out_by/id
                // This .UsedFor must exist, created in first pass
                foreach (var id in keyword.Value)
                {
                    var book = allWorks[id];
                    book.About ??= [];
                    book.About.Add(subjectRef);
                }
            }

        }

        private static void AssignPublishers(Dictionary<string, LinguisticObject> allWorks, Dictionary<string, ParsedAgent> publisherDict)
        {
            const string yalePmc = "Published for The Paul Mellon Centre for Studies in British Art by Yale University Press";

            var conn = DbCon.Get();
            var yaleUP = conn.GetAuthorityFromSourceString("publisher", "Yale University Press", false);
            Writer.WriteToDisk(yaleUP!.GetFull()!);
            var yaleUPRef = yaleUP!.GetReference() as Group;

            foreach (var publisher in publisherDict)
            {
                var publisherString = publisher.Value.NormalisedOriginal;
                Group? groupRef = null;
                Group? full = null;
                if (publisherString.StartsWith(yalePmc))
                {
                    groupRef = yaleUPRef;
                }
                else
                {
                    var authority = conn.GetAuthorityFromSourceString("publisher", publisherString, true);
                    if (authority == null)
                    {
                        throw new InvalidOperationException("Must have an Authority at this point");
                    }
                    if (authority.Unreconciled)
                    {
                        authority.Type = "Group";
                        authority.Label = publisherString;
                    }
                    groupRef = authority.GetReference() as Group;
                    full = authority.GetFull() as Group;

                    if (groupRef == null || full == null)
                    {
                        throw new InvalidOperationException("Must have a Group at this point");
                    }
                    Writer.WriteToDisk(full);
                }

                // /used_for[classified_as=PUBLISHING]/carried_out_by/id
                // This .UsedFor must exist, created in first pass
                foreach (var id in publisher.Value.Identifiers)
                {
                    var book = allWorks[id];
                    book.UsedFor![0].CarriedOutBy = [groupRef];
                }
            }
        }

        private static void AssignPlaces(Dictionary<string, LinguisticObject> allWorks, Dictionary<string, List<string>> placeDict)
        {
            var conn = DbCon.Get();

            foreach (var place in placeDict)
            {
                var placeString = place.Key;
                Place? placeRef = null;
                Place? full = null;

                var authority = conn.GetAuthorityFromSourceString("place", placeString, true);
                if (authority == null)
                {
                    throw new InvalidOperationException("Must have an Authority at this point");
                }
                if (authority.Unreconciled)
                {
                    authority.Type = "Place";
                    authority.Label = placeString;
                }
                placeRef = authority.GetReference() as Place;
                full = authority.GetFull() as Place;

                if (placeRef == null || full == null)
                {
                    throw new InvalidOperationException("Must have a Place at this point");
                }
                Writer.WriteToDisk(full);


                // /used_for[classified_as=PUBLISHING]/took_place_at/id
                // This .UsedFor must exist, created in first pass
                foreach (var id in place.Value)
                {
                    var book = allWorks[id];
                    book.UsedFor![0].TookPlaceAt = [placeRef];
                }
            }
        }

        private static void AssignPersAuthors(
            Dictionary<string, LinguisticObject> allWorks, 
            Dictionary<string, List<HumanMadeObject>> allHMOs, 
            Dictionary<string, ParsedAgent> persAuthorFullDict)
        {
            var conn = DbCon.Get();

            foreach (var person in persAuthorFullDict)
            {
                var personString = person.Value.NormalisedOriginal;
                Person? personRef = null;
                Person? full = null;

                var authority = conn.GetAuthorityFromSourceString("persauthorfull", personString, true);
                if (authority == null)
                {
                    throw new InvalidOperationException("Must have an Authority at this point");
                }
                if (authority.Unreconciled)
                {
                    authority.Type = "Person";
                    authority.Label = person.Value.NormalisedLocForm;
                }
                personRef = authority.GetReference() as Person;
                full = authority.GetFull() as Person;

                if (personRef == null || full == null)
                {
                    throw new InvalidOperationException("Must have a Person at this point");
                }
                Writer.WriteToDisk(full);

                var role = person.Value.Role ?? "author";
                var activity = MappedRole.GetActivityWithPart(role);

                foreach (var id in person.Value.Identifiers)
                {
                    if (person.Value.Role == "binder" || person.Value.Role == "printer")
                    {
                        foreach (var hmo in allHMOs[id])
                        {
                            var book = allWorks[id];
                            book.CreatedBy ??= new Activity(Types.Production);
                            book.CreatedBy.Part ??= [];
                            book.CreatedBy.Part.Add(new Activity(activity.Part![0].Type!)
                            {
                                CarriedOutBy = [personRef],
                                ClassifiedAs = activity.Part[0].ClassifiedAs
                            });
                        }
                    }
                    else
                    {
                        var book = allWorks[id];
                        book.CreatedBy ??= new Activity(Types.Creation);
                        book.CreatedBy.Part ??= [];
                        book.CreatedBy.Part.Add(new Activity(activity.Part![0].Type!)
                        {
                            CarriedOutBy = [personRef],
                            ClassifiedAs = activity.Part[0].ClassifiedAs
                        });
                    }
                }
            }
        }

        [Obsolete]
        private static void OldPersonReconciliationObsolete(Dictionary<string, LinguisticObject> allWorks, Dictionary<string, List<HumanMadeObject>> allHMOs, Dictionary<string, ParsedAgent> persAuthorFullDict)
        {
            // Now split people into roles and dates and do similar as above.
            // And work out how to reconcile with Getty and LoC.
            // We might have the same person but with different "roles"
            // Or the same person with and without dates (probe for this)
            // name, parts [dates] (role)
            const string noRole = "%%NO_ROLE%%";
            const string noDates = "%%NO_DATES%%";

            var libraryPeople = new List<LibraryPersonName>();

            // for spelunking
            var peopleWithoutRoles = new List<string>();
            var peopleWithoutDates = new List<string>();

            foreach (var pKey in persAuthorFullDict)
            {
                var person = pKey.Value;
                if (person.Role == null)
                {
                    peopleWithoutRoles.Add(pKey.Key);
                }
                if (person.DateString == null)
                {
                    peopleWithoutDates.Add(pKey.Key);
                }

                var libraryPerson = libraryPeople.SingleOrDefault(p => p.Name == person.Name);
                if (libraryPerson == null)
                {
                    libraryPerson = new LibraryPersonName() { Name = person.Name };
                    libraryPeople.Add(libraryPerson);
                }

                string datesKey = person.DateString ?? noDates;
                if (!libraryPerson.DateBuckets.ContainsKey(datesKey))
                {
                    libraryPerson.DateBuckets[datesKey] = [];
                }
                var booksByRole = libraryPerson.DateBuckets[datesKey];

                string roleKey = person.Role ?? noRole;
                if (!booksByRole.ContainsKey(roleKey))
                {
                    booksByRole[roleKey] = [];
                }
                booksByRole[roleKey].AddRange(pKey.Value.Identifiers);
            }

            Console.WriteLine("peopleWithoutRoles.Count: " + peopleWithoutRoles.Count);
            Console.WriteLine("peopleWithoutDates.Count: " + peopleWithoutDates.Count);
            // LogNameDateCounts(noDates, libraryPeople);


            // Now see if we can collapse some of these names
            // TODO: In this first pass I'm going to ignore dates completely, other than choosing the first non-empty date,
            // and just collapse on name part match
            // This will be right most of the time, but is not good enough...

            // TODO - Possibly using dates, reconcile with LOC / AAT
            // Add reconciled to equivalent
            // For unmatched - have a local one? Parse the dates?
            var normalisedLibraryPeople = new List<NormalisedLibraryPersonName>();
            foreach (var libraryPerson in libraryPeople)
            {
                var normalised = new NormalisedLibraryPersonName()
                {
                    Name = libraryPerson.Name,
                    Date = libraryPerson.DateBuckets.Keys.FirstOrDefault(x => x != noDates)
                };
                foreach (var dateBucket in libraryPerson.DateBuckets)
                {
                    foreach (var roleDict in dateBucket.Value)
                    {
                        var role = roleDict.Key;
                        var booksInRole = roleDict.Value;
                        if (!normalised.RolesToBooks.ContainsKey(role))
                        {
                            normalised.RolesToBooks[role] = [];
                        }
                        normalised.RolesToBooks[role].AddRange(booksInRole);
                    }
                }
                normalisedLibraryPeople.Add(normalised);
            }


            // Now we can create Person records and assert in book record.
            // TODO - This needs to be a permanent IF
            int personIdMinter = 1;
            foreach (var libraryPerson in normalisedLibraryPeople)
            {
                var person = new Person()
                    .WithId(Identity.PeopleBase + personIdMinter++)
                    .WithLabel(libraryPerson.Name);

                foreach (var roleBooks in libraryPerson.RolesToBooks)
                {
                    var activity = MappedRole.GetActivityWithPart(roleBooks.Key);

                    foreach (var id in roleBooks.Value)
                    {
                        if (roleBooks.Key == "binder" || roleBooks.Key == "printer")
                        {
                            foreach (var hmo in allHMOs[id])
                            {
                                var book = allWorks[id];
                                book.CreatedBy ??= new Activity(Types.Production);
                                book.CreatedBy.Part ??= [];
                                book.CreatedBy.Part.Add(new Activity(activity.Part![0].Type!)
                                {
                                    CarriedOutBy = [person],
                                    ClassifiedAs = activity.Part[0].ClassifiedAs
                                });
                            }
                        }
                        else
                        {
                            var book = allWorks[id];
                            book.CreatedBy ??= new Activity(Types.Creation);
                            book.CreatedBy.Part ??= [];
                            book.CreatedBy.Part.Add(new Activity(activity.Part![0].Type!)
                            {
                                CarriedOutBy = [person],
                                ClassifiedAs = activity.Part[0].ClassifiedAs
                            });
                        }
                    }
                }
            }
        }



        public static void AssignCorpAuthors(
            Dictionary<string, LinguisticObject> allWorks, 
            Dictionary<string, ParsedAgent> corpAuthorDict)
        {
            var conn = DbCon.Get();

            foreach (var corpAuthor in corpAuthorDict)
            {
                var corpAuthorString = corpAuthor.Value.NormalisedOriginal;
                Group? groupRef = null;
                Group? full = null;
                if (corpAuthorString.StartsWith(Locations.PhotoArchiveName))
                {
                    groupRef = Locations.PhotoArchiveGroupRef;
                }
                else
                {
                    var authority = conn.GetAuthorityFromSourceString("corpauthor", corpAuthorString, true);
                    if (authority == null)
                    {
                        throw new InvalidOperationException("Must have an Authority at this point");
                    }
                    if(authority.Unreconciled)
                    {
                        authority.Type = "Group";
                        authority.Label = corpAuthorString;
                    }
                    groupRef = authority.GetReference() as Group;
                    full = authority.GetFull() as Group;

                    if(groupRef == null || full == null)
                    {
                        throw new InvalidOperationException("Must have a Group at this point");
                    }
                    Writer.WriteToDisk(full);
                }

                foreach (var id in corpAuthor.Value.Identifiers)
                {
                    var work = allWorks[id];
                    work.CreatedBy ??= new Activity(Types.Creation);
                    work.CreatedBy.Part ??= [];
                    work.CreatedBy.Part.Add(new Activity(Types.Creation)
                    {
                        CarriedOutBy = [groupRef]
                    });
                }
            }
        }


        private static void LogNameDateCounts(string noDates, List<LibraryPersonName> libraryPeople)
        {
            Dictionary<int, int> nameDateCounts = new Dictionary<int, int>();
            foreach (var lp in libraryPeople)
            {
                var count = lp.DateBuckets.Count(x => x.Key != noDates);
                if (!nameDateCounts.ContainsKey(count))
                {
                    nameDateCounts[count] = 0;
                }
                nameDateCounts[count] = nameDateCounts[count] + 1;
                if (count == 3)
                {
                    Console.WriteLine(lp.Name);
                }
            }
            foreach (int count in nameDateCounts.Keys)
            {
                Console.WriteLine($"People with {count} date(s): {nameDateCounts[count]}");
            }
        }

        private static void Sample(
            Dictionary<string, LinguisticObject> allWorks,
            Dictionary<string, List<HumanMadeObject>> allHMOs,
            int interval, bool writeToDisk)
        {
            List<string> pleaseDump = [
                "0955581036",
                "030020969X",
                "0953238997",
                "0300059868"
            ];
            var options = new JsonSerializerOptions { WriteIndented = true, };
            int count = 0;
            foreach (var work in allWorks)
            {
                if (count % interval == 0 || pleaseDump.Contains(work.Key))
                {
                    var generatedJson = JsonSerializer.Serialize(work.Value, options);
                    Console.WriteLine(generatedJson);

                    if (writeToDisk)
                    {
                        var workJson = JsonSerializer.Serialize(work.Value, options);
                        File.WriteAllText($"../../../output/library/linguistic/{work.Key}.json", workJson);
                        int counter = 1;
                        foreach (var hmo in allHMOs[work.Key])
                        {
                            var hmoJson = JsonSerializer.Serialize(hmo, options);
                            File.WriteAllText($"../../../output/library/hmo/{work.Key}-{counter++}.json", hmoJson);
                        }
                    }
                }
                count++;
            }
        }

    }
}
