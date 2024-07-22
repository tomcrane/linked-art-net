using LinkedArtNet.Parsers;
using LinkedArtNet.Vocabulary;
using LinkedArtNet;
using PmcTransformer.Helpers;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Text.Json;
using Group = LinkedArtNet.Group;

namespace PmcTransformer.Library
{
    public class Processor
    {
        private static readonly char[] separator = [',', ' '];

        public static void ProcessLibrary(XDocument xLibrary)
        {
            var timespanParser = new TimespanParser();

            // Maps
            var allWorks = new Dictionary<string, LinguisticObject>();
            var allHMOs = new Dictionary<string, List<HumanMadeObject>>();

            // For reconciliation
            var persAuthorFullDict = new Dictionary<string, List<string>>();
            var corpAuthorDict = new Dictionary<string, List<string>>();
            var publisherDict = new Dictionary<string, List<string>>();
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

                // The first iteration will focus only on the books. 
                var work = new LinguisticObject()
                    .WithContext()
                    .WithId($"{Identity.LibraryLinguistic}{id}");
                allWorks.Add(id, work);

                // /identified_by[type = Name, classified_as = PRIMARY] / value
                var title = record.LibStrings("title").Single();
                work.IdentifiedBy = [
                    new Identifier(id).AsSystemAssignedNumber(),
                    new Name(title).AsPrimaryName(),
                ];

                // Now create 1 or more HumanMadeObjects for the Work,
                // based on the parallel accloc (location) and accnofld (accession number) fields.
                var acclocs = record.LibStrings("accloc").ToList();
                var accessionNumbers = record.LibStrings("accnofld").ToList();
                accLocCounter.IncrementCounter(acclocs.Count);

                if (accessionNumbers.Count > 0 && acclocs.Count != accessionNumbers.Count)
                {
                    throw new InvalidOperationException("Mismatch accloc/accnofld for " + id);
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
                            .WithId($"{Identity.LibraryHmo}{id}-{i + 1}");
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
                        var normalisedMedium = normalisedMediums.FirstOrDefault(m => m.StartsWith(mediumClass));
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
                            // This class value is an Identifier
                            work.IdentifiedBy.Add(new Identifier(classVal));
                        }
                    }
                }


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
                    var places = record.LibStrings("place");
                    foreach (var place in places)
                    {
                        hasPlace = true;
                        placeDict.AddToListForKey(place.TrimOuterBrackets(), id);
                    }
                }

                bool hasPublisher = false;
                // publisher
                // /used_for[classified_as=PUBLISHING]/carried_out_by/id
                var publishers = record.LibStrings("publisher");
                foreach (var publisher in publishers)
                {
                    hasPublisher = true;
                    publisherDict.AddToListForKey(publisher.TrimOuterBrackets(), id);
                }

                var year = record.LibStrings("year").SingleOrDefault();
                var nobrackets = year.TrimOuterBrackets();
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


                // =====================
                // Entities to be reconciled
                // =====================

                // Repeatable. Personal name of a creator/contributor to the Work, plus the role they played. See notes on People below.
                // /created_by/part/carried_out_by/id (for person)
                // /created_by/part/classified_as/id (for role)
                var persAuthors = record.LibStrings("persauthorfull");
                foreach (var author in persAuthors)
                {
                    persAuthorFullDict.AddToListForKey(author.TrimOuterBrackets(), id);
                }

                // corpauthor - Repeatable. Organization/Corporate name of a creator/contributor to the Work. No role provided, assume role=author.
                // See notes on Groups below.
                // /created_by/part/carried_out_by/id
                var corpAuthors = record.LibStrings("corpauthor");
                foreach (var author in corpAuthors)
                {
                    corpAuthorDict.AddToListForKey(author.TrimOuterBrackets(), id);
                }

                // See example output E2865, D8326
                // /about/id
                var keywords = record.LibStrings("keywords");
                foreach (var keyword in keywords)
                {
                    keywordDict.AddToListForKey(keyword.TrimOuterBrackets(), id);
                }
            }

            // #######################################################################################
            // ####################### Finished first pass through all records #######################
            // #######################################################################################

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();


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



            
            CorpAuthors.ReconcileCorpAuthors(allWorks, corpAuthorDict);



            // Now split people into roles and dates and do similar as above.
            // And work out how to reconcile with Getty and LoC.
            // We might have the same person but with different "roles"
            // Or the same person with and without dates (probe for this)
            // name, parts [dates] (role)
            string rolePattern = @"^(.*)\((.*)\)$";
            string datePattern = @"^(.*)\[(.*)\]$";
            const string noRole = "%%NO_ROLE%%";
            const string noDates = "%%NO_DATES%%";

            var libraryPeople = new List<LibraryPersonName>();

            // for spelunking
            var peopleWithoutRoles = new List<string>();
            var peopleWithoutDates = new List<string>();

            foreach (var person in persAuthorFullDict)
            {
                string personDatePart;
                string personPart;
                string rolePart = noRole;
                string datePart = noDates;
                var roleMatch = Regex.Match(person.Key, rolePattern);
                if (roleMatch.Success)
                {
                    personDatePart = roleMatch.Groups[1].Value.Trim();
                    rolePart = roleMatch.Groups[2].Value.Trim();
                }
                else
                {
                    peopleWithoutRoles.Add(person.Key);
                    personDatePart = person.Key;
                }

                var dateMatch = Regex.Match(personDatePart, datePattern);
                if (dateMatch.Success)
                {
                    personPart = dateMatch.Groups[1].Value.Trim();
                    datePart = dateMatch.Groups[2].Value.Trim();
                    var tidyDate = TidyPersonDate(datePart);
                    if (tidyDate != datePart)
                    {
                        Console.WriteLine($"Tidied \"{datePart}\" to \"{tidyDate}\" for {person.Key}");
                        datePart = tidyDate;
                    }
                }
                else
                {
                    peopleWithoutDates.Add(personDatePart);
                    personPart = personDatePart;
                }

                var libraryPerson = libraryPeople.SingleOrDefault(p => p.Name == personPart);
                if (libraryPerson == null)
                {
                    libraryPerson = new LibraryPersonName() { Name = personPart };
                    libraryPeople.Add(libraryPerson);
                }

                if (!libraryPerson.DateBuckets.ContainsKey(datePart))
                {
                    libraryPerson.DateBuckets[datePart] = [];
                }
                var booksByRole = libraryPerson.DateBuckets[datePart];

                if (!booksByRole.ContainsKey(rolePart))
                {
                    booksByRole[rolePart] = [];
                }
                booksByRole[rolePart].AddRange(person.Value);
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

            Console.WriteLine("ROLES===============");
            foreach (var role in MappedRole.Roles)
            {
                Console.WriteLine($"{role.Key}: {role.Value}");
            }

            // Now we can create Place records and assert in book record.
            // Unlike people and groups I think we should reconcile these immediately - no local identity at all
            // ... as we don't expect non-reconcilable or PMC-only places (or do we?)
            // But still key recorded place in DB.
            int placeIdMinter = 1;
            foreach (var kvp in placeDict)
            {
                var place = new Place()
                    .WithId(Identity.PlaceBase + "temp-" + placeIdMinter++)
                    .WithLabel(kvp.Key);

                // /used_for[classified_as=PUBLISHING]/took_place_at/id
                // This .UsedFor must exist, created in first pass
                foreach (var id in kvp.Value)
                {
                    var book = allWorks[id];
                    book.UsedFor![0].TookPlaceAt = [place];
                }
            }


            // .. temporarily same for publishers...
            int publisherIdMinter = 1;
            const string yalePmc = "Published for The Paul Mellon Centre for Studies in British Art by Yale University Press";
            foreach (var kvp in publisherDict)
            {
                var group = new Group()
                    .WithId(Identity.GroupBase + "temp-" + publisherIdMinter++)
                    .WithLabel(kvp.Key);

                // /used_for[classified_as=PUBLISHING]/carried_out_by/id
                // This .UsedFor must exist, created in first pass
                foreach (var id in kvp.Value)
                {
                    var book = allWorks[id];
                    book.UsedFor![0].CarriedOutBy = [group];
                }
            }



            // .. temporarily same for keywords...
            int keywordIdMinter = 1;
            foreach (var kvp in keywordDict)
            {
                // These need to be reconciled to any kind of entity - people, places, concepts
                var thing = new LinkedArtObject(Types.Type)
                    .WithId(Identity.ConceptBase + "temp-" + keywordIdMinter++)
                    .WithLabel(kvp.Key);

                // /used_for[classified_as=PUBLISHING]/carried_out_by/id
                // This .UsedFor must exist, created in first pass
                foreach (var id in kvp.Value)
                {
                    var book = allWorks[id];
                    book.About ??= [];
                    book.About.Add(thing);
                }
            }

            // once serialised in short form, add context and add reconciled equivalents and serialise as groups/people  
            // add in person dates
            // make use of active string in date field (currently being stripped)
            // Sample(allWorks, allHMOs, 1000, true);

        }

        private static string TidyPersonDate(string datePart)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in datePart)
            {
                if (c == '-' || char.IsDigit(c))
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
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
