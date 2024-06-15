using LinkedArtNet;
using LinkedArtNet.Vocabulary;
using PmcTransformer.Helpers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
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
                    persAuthorFullDict.AddToListForKey(author.Value.TrimSpecial(), id);
                }


                // corpauthor - Repeatable. Organization/Corporate name of a creator/contributor to the Work. No role provided, assume role=author.
                // See notes on Groups below.
                // /created_by/part/carried_out_by/id
                var corpAuthors = record.Elements(libNs + "corpauthor");
                foreach (var author in corpAuthors)
                {
                    corpAuthorDict.AddToListForKey(author.Value.TrimSpecial(), id);
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
                // 3: 159
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
                            Console.WriteLine("Book " + id + " already has a location");
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


                // place
                // /used_for[classified_as=PUBLISHING]/took_place_at/id
                var places = record.Elements(libNs + "place");
                foreach (var place in places)
                {
                    placeDict.AddToListForKey(place.Value.TrimSpecial(), id);
                }

                // publisher
                // /used_for[classified_as=PUBLISHING]/carried_out_by/id
                var publishers = record.Elements(libNs + "publisher");
                foreach (var publisher in publishers)
                {
                    publisherDict.AddToListForKey(publisher.Value.TrimSpecial(), id);
                }

                // also need year for publishing ^^ these three make a new Activity, see koot.UsedFor

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

                foreach (var id in kvp.Value)
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
