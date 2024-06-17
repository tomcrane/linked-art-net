﻿using LinkedArtNet;
using LinkedArtNet.Parsers;
using LinkedArtNet.Vocabulary;
using PmcTransformer.Helpers;
using System.Diagnostics.Metrics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Group = LinkedArtNet.Group;

namespace PmcTransformer
{
    internal class Program
    {
        static JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true, };

        static void Main(string[] args)
        {
            var root = "C:\\Users\\TomCrane\\Dropbox\\digirati\\PMC\\linked.art\\2024-03-18";
            var archive = root + "\\2024-03-11_archive";
            var library = root + "\\2024-03-11_library";
            var photo_archive = root + "\\2024-03-14_photo-archive";

            XNamespace libNs = "x-schema:EF-34074-Export.dtd";

            StreamReader reader = new StreamReader(library + "\\2024-03-11_library.xml", Encoding.UTF8);
            var xLibrary = XDocument.Load(reader);

            var timespanParser = new TimespanParser();

            // Common Types
            // Verify correct term "EDITION_STMT" https://vocab.getty.edu/aat/300435435
            var editionDescription = Getty.AatType("Edition", "300435435");



            // Maps
            var allBooks = new Dictionary<string, HumanMadeObject>();
            var persAuthorFullDict = new Dictionary<string, List<string>>();
            var corpAuthorDict = new Dictionary<string, List<string>>();
            int nullMediumCounter = 0;
            var classCounter = new Dictionary<int, int>();
            var accLocCounter = new Dictionary<int, int>();
            var multipleAccLocCounter = new Dictionary<int, int>();
            int classMatchesAccLocCounter = 0;
            int classHasLocationButDifferentFromAcclocCounter = 0;
            var placeDict = new Dictionary<string, List<string>>();
            var publisherDict = new Dictionary<string, List<string>>();
            var accnofldCounter = new Dictionary<int, int>();
            var collationCounter = new Dictionary<int, int>();
            var keywordDict = new Dictionary<string, List<string>>();


            foreach (var record in xLibrary.Root!.Elements())
            {
                // RS: /identified_by[type=Identifier,classified_as=REPOSITORY]/value
                var id = record.Attribute("ID")!.Value;

                // "Missing record created by data verification program"
                if (id == "Q$") continue;

                // The first iteration will focus only on the books. 
                var book = new HumanMadeObject()
                    .WithContext()
                    .WithId($"{Identity.BooksBase}{id}");

                allBooks.Add(id, book);

                // RS: /identified_by[type = Name, classified_as = PRIMARY] / value
                var title = record.Elements(libNs + "title").Single().Value;

                book.IdentifiedBy = [
                    new Identifier(id).AsSystemAssignedNumber(),
                    new Identifier(title).AsPrimaryName(),
                ];

                // RS: /referred_to_by[type=LinguisticObject,classified_as=EDITION_STMT]/value
                var edition = record.Elements(libNs + "edition").Single().Value;
                if (!string.IsNullOrWhiteSpace(edition))
                {
                    book.ReferredToBy = [
                        new LinguisticObject()
                            .WithContent(edition)
                            .WithClassifiedAs(editionDescription)
                    ];
                }

                // Repeatable. Personal name of a creator/contributor to the Work, plus the role they played. See notes on People below.
                // /created_by/part/carried_out_by/id (for person)
                // /created_by/part/classified_as/id (for role)
                var persAuthors = record.Elements(libNs + "persauthorfull");
                foreach (var author in persAuthors)
                {
                    persAuthorFullDict.AddToListForKey(author.Value.TrimOuterBrackets(), id);
                }


                // corpauthor - Repeatable. Organization/Corporate name of a creator/contributor to the Work. No role provided, assume role=author.
                // See notes on Groups below.
                // /created_by/part/carried_out_by/id
                var corpAuthors = record.Elements(libNs + "corpauthor");
                foreach (var author in corpAuthors)
                {
                    corpAuthorDict.AddToListForKey(author.Value.TrimOuterBrackets(), id);
                }


                // Observed so far ALL records have exactly one medium.
                // /classified_as/id
                var medium = record.Elements(libNs + "medium").Single().Value;
                var mediumClassifier = Media.FromRecordValue(medium);
                if(mediumClassifier == null)
                {
                    // Image files
                    nullMediumCounter++;
                } 
                else
                {
                    book.WithClassifiedAs(mediumClassifier);
                }

                // See notes (messy)
                // /identified_by[type=Identifier]/value   OR   /current_location/id
                // Distribution of class values:
                // Unfiltered        Filtered as linq below
                // 0: 0              15889
                // 1: 38863          26457
                // 2: 24481          21009
                // 3: 39             28
                // 4: 4              4
                // 5: 1              1
                // 9: 2              2
                // 12: 1             1
                var classes = record.Elements(libNs + "class")
                    .Select(c => c.Value)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Where(v => v != "AUCTION CATALOGUES")
                    .Where(v => !v.StartsWith("IN PROCESS"))
                    .ToList();
                classCounter.IncrementCounter(classes.Count);

                // /current_location/id
                // Distribution of accloc values (book is in more than one physical location...):
                // 1: 62088
                // 2: 981
                // 3: 159  // see 0955581036
                // 4: 89
                // 5: 24
                // 6: 20
                // 7: 6
                // 8: 6
                // 9: 3
                // 11: 1
                // 12: 5
                // 14: 3
                // 16: 1
                // 18: 1
                // 22: 1
                // 30: 1
                // 34: 1
                // 37: 1
                var acclocs = record.Elements(libNs + "accloc")
                    .Select(c => c.Value)
                    .ToList();
                accLocCounter.IncrementCounter(acclocs.Count);

                var alreadyMappedLocations = new HashSet<string>();   
                foreach(var location in acclocs)
                {
                    if (alreadyMappedLocations.Contains(location))
                    {
                        continue;
                    }
                    alreadyMappedLocations.Add(location);
                    var mappedLocation = Locations.FromRecordValue(location);
                    if(mappedLocation != null)
                    {
                        if (book.CurrentLocation == null)
                        {
                            book.CurrentLocation = mappedLocation;
                        }
                        else
                        {
                            // Console.WriteLine("Book " + id + " already has a location");
                        }
                    }
                }
                multipleAccLocCounter.IncrementCounter(alreadyMappedLocations.Count);

                var classesThatWillBecomeIdentifiers = new List<string>();
                foreach (var classVal in classes)
                {
                    var locationAsClass = Locations.FromRecordValue(classVal, true);
                    if(locationAsClass != null)
                    {
                        if (alreadyMappedLocations.Contains(classVal))
                        {
                            classMatchesAccLocCounter++;
                        }
                        else
                        {
                            classHasLocationButDifferentFromAcclocCounter++;
                        }
                    }
                    else
                    {
                        // not a known location
                        // Console.WriteLine($"{id}: {classVal}");
                        classesThatWillBecomeIdentifiers.Add(classVal);
                    }
                }

                if(classesThatWillBecomeIdentifiers.Any())
                {
                    book.IdentifiedBy ??= [];
                    book.IdentifiedBy.Add(new Identifier(string.Join(' ', classesThatWillBecomeIdentifiers)));
                }

                bool hasPlace = false;
                // place
                // /used_for[classified_as=PUBLISHING]/took_place_at/id
                var places = record.Elements(libNs + "place");
                foreach (var place in places)
                {
                    hasPlace = true;
                    placeDict.AddToListForKey(place.Value.TrimOuterBrackets(), id);
                }

                bool hasPublisher = false;
                // publisher
                // /used_for[classified_as=PUBLISHING]/carried_out_by/id
                var publishers = record.Elements(libNs + "publisher");
                foreach (var publisher in publishers)
                {
                    hasPublisher = true;
                    publisherDict.AddToListForKey(publisher.Value.TrimOuterBrackets(), id);
                }

                var yearEl = record.Elements(libNs + "year").SingleOrDefault()?.Value;
                var year = yearEl;
                if (string.IsNullOrWhiteSpace(year)) year = null;
                var nobrackets = year.TrimOuterBrackets();
                if (nobrackets != null && nobrackets.StartsWith("n.d")) year = null;
                if (nobrackets == "Date of publication not identified") year = null;

                
                if (id == "00328898")
                {
                    // Console.WriteLine("pause");
                }
                var timespan = timespanParser.Parse(year);

                //Console.WriteLine(yearEl);
                //Console.WriteLine(JsonSerializer.Serialize(timespan, options));

                if(year != null || hasPublisher || hasPlace)
                {
                    if (year != null && !char.IsDigit(year[0]))
                    {
                        //Console.WriteLine(year);
                    }
                    // create a publishing activity which we can populate with reconciled place and publisher later
                    book.UsedFor = [
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

                // accnofld 
                // /identified_by[type=Identifier,classified_as=ACCESSION]/value
                // Distribution of accessionNumbers:
                // This looks uncannily similar to Distribution of accloc values!
                // 1: 62088
                // 2: 981
                // 3: 159  // see 0955581036
                // 4: 89
                // 5: 24
                // 6: 20
                // 7: 6
                // 8: 6
                // 9: 3
                // 11: 1
                // 12: 5
                // 14: 3
                // 16: 1
                // 18: 1
                // 22: 1
                // 30: 1
                // 34: 1
                // 37: 1
                var accessionNumbers = record.Elements(libNs + "accnofld")
                    .Select(c => c.Value)
                    .ToList();
                accnofldCounter.IncrementCounter(accessionNumbers.Count);
                foreach(var accessionNumer in accessionNumbers)
                {
                    // unlike locations we can allocate multiple accession numbers...
                    // but should we?
                    book.IdentifiedBy.Add(new Identifier(accessionNumer).AsAccessionNumber());
                }

                // collation
                // /referred_to_by[classified_as=COLLATION/value
                var collations = record.Elements(libNs + "collation")
                    .Select(c => c.Value)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .ToList();
                collationCounter.IncrementCounter(collations.Count);
                if (collations.Count == 1)
                {
                    // There is only ever none or one
                    var collationStatement = new LinguisticObject()
                        .WithClassifiedAs(
                            Getty.AatType("Collations Statement", "300311715"),  // Check this is the right one
                            Getty.AatType("Brief Text", "300418049"))
                        .WithContent(collations[0]);

                    book.ReferredToBy ??= [];
                    book.ReferredToBy.Add(collationStatement);
                }

                // Unreconciled!
                // See example output E2865, D8326
                // /about/id
                var keywords = record.Elements(libNs + "keywords");
                foreach (var keyword in keywords)
                {
                    keywordDict.AddToListForKey(keyword.Value.TrimOuterBrackets(), id);
                }

                // entrydate
                // TODO: use in Activity Stream - in DB

                // series - this is a statement
                // /referred_to_by[classified_as=???]/value
                var series = record.Elements(libNs + "series")
                    .Select(c => c.Value)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .SingleOrDefault();
                var seriesno = record.Elements(libNs + "seriesno")
                    .Select(c => c.Value)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .SingleOrDefault();
                if (series != null)
                {
                    if(seriesno != null)
                    {
                        series += " " + seriesno;
                    }
                    var seriesStatement = new LinguisticObject()
                        .WithClassifiedAs(
                            Getty.AatType("Series", "300027349"),  // THIS IS ALMOST CERTAINLY WRONG
                            Getty.AatType("Brief Text", "300418049"))
                        .WithContent(series);

                    book.ReferredToBy ??= [];
                    book.ReferredToBy.Add(seriesStatement);
                }

                // lng

                // notescsvx 

                // afilecsvx 




            }

            // #######################################################################################
            // ####################### Finished first pass through all records #######################
            // #######################################################################################

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();

            Console.WriteLine("nullMediumCounter: " + nullMediumCounter);
            Console.WriteLine("allBooks keys: " + allBooks.Keys.Count);
            Console.WriteLine("persauthorfull keys: " + persAuthorFullDict.Keys.Count);
            Console.WriteLine("corpAuthor keys: " + corpAuthorDict.Keys.Count);
            classCounter.Display("Distribution of class values:");
            accLocCounter.Display("Distribution of accloc values:");
            
            Console.WriteLine("Temporarily mapped UO, QUA, DUP");
            Locations.ShowUnmapped();

            // What to do...
            multipleAccLocCounter.Display("Books with more than one distinct accloc:");


            Console.WriteLine("classMatchesAccLocCounter: " + classMatchesAccLocCounter);
            Console.WriteLine("classHasLocationButDifferentFromAcclocCounter: " + classHasLocationButDifferentFromAcclocCounter);


            Console.WriteLine("place keys: " + placeDict.Keys.Count);
            Console.WriteLine("publisher keys: " + publisherDict.Keys.Count);
            accnofldCounter.Display("Distribution of accessionNumbers:");
            collationCounter.Display("Distribution of collations:");




            // Create Groups for corpauthor and assert in book record.
            // TODO - this needs to be consistent between runs so once we are sure about our corporation,
            // mint a permanent id for it and store in DB
            int corpIdMinter = 1;
            foreach (var corpAuthor in corpAuthorDict)
            {
                var group = new Group()
                    .WithId(Identity.GroupBase + corpIdMinter++)
                    .WithLabel(corpAuthor.Key);

                foreach (var id in corpAuthor.Value)
                {
                    var book = allBooks[id];
                    book.CreatedBy ??= new Activity(Types.Creation);
                    book.CreatedBy.Part ??= [];
                    book.CreatedBy.Part.Add(new Activity(Types.Creation)
                    {
                        CarriedOutBy = [group]
                    });
                }
            }

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
                    if(tidyDate != datePart)
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
                foreach(var dateBucket in libraryPerson.DateBuckets)
                {
                    foreach(var roleDict in dateBucket.Value)
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

                    foreach(var id in roleBooks.Value)
                    {
                        var book = allBooks[id];
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
            foreach(var role in MappedRole.Roles)
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
                    var book = allBooks[id];
                    book.UsedFor![0].TookPlaceAt = [place];
                }
            }


            // .. temporarily same for publishers...
            int publisherIdMinter = 1;
            foreach (var kvp in publisherDict)
            {
                var group = new Group()
                    .WithId(Identity.GroupBase + "temp-" + publisherIdMinter++)
                    .WithLabel(kvp.Key);

                // /used_for[classified_as=PUBLISHING]/carried_out_by/id
                // This .UsedFor must exist, created in first pass
                foreach (var id in kvp.Value)
                {
                    var book = allBooks[id];
                    book.UsedFor![0].CarriedOutBy = [group];
                }
            }



            // .. temporarily same for keywords...
            int keywordIdMinter = 1;
            foreach (var kvp in keywordDict)
            {
                // These need to be reconciled to any kind of entity - people, places, concepts
                var thing = new LinkedArtObject()
                    .WithId(Identity.GroupBase + "temp-" + keywordIdMinter++)
                    .WithLabel(kvp.Key);

                // /used_for[classified_as=PUBLISHING]/carried_out_by/id
                // This .UsedFor must exist, created in first pass
                foreach (var id in kvp.Value)
                {                    
                    var book = allBooks[id];
                    book.About ??= [];
                    book.About.Add(thing);
                }
            }

            // once serialised in short form, add context and add reconciled equivalents and serialise as groups/people  
            // add in person dates
            // make use of active string in date field (currently being stripped)
            Sample(allBooks, 1000, true);
        }



        private static string TidyPersonDate(string datePart)
        {
            StringBuilder sb = new StringBuilder();
            foreach(char c in datePart)
            {
                if(c == '-' || char.IsDigit(c))
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

        private static void Sample(Dictionary<string, HumanMadeObject> allBooks, int interval, bool writeToDisk)
        {
            var options = new JsonSerializerOptions { WriteIndented = true, };
            int count = 0;
            foreach(var book in allBooks)
            {
                if(count % interval == 0)
                {
                    var generatedJson = JsonSerializer.Serialize(book.Value, options);
                    Console.WriteLine(generatedJson);
                    if(writeToDisk)
                    {
                        var json = JsonSerializer.Serialize(book.Value, options);
                        File.WriteAllText($"../../../output/library/books/{book.Key}.json", json);
                    }
                }
                count++;
            }
        }


    }
}