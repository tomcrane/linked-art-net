using LinkedArtNet;
using LinkedArtNet.Vocabulary;
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

            var baseUrl = "https://linkedart.paul-mellon-centre.ac.uk/";
            var booksBase = $"{baseUrl}library/books/";
            var groupBase = $"{baseUrl}groups/";
            var peopleBase = $"{baseUrl}people/";

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


            foreach (var record in xLibrary.Root.Elements())
            {
                bool outputAnyway = false;

                // RS: /identified_by[type=Identifier,classified_as=REPOSITORY]/value
                var id = record.Attribute("ID").Value;

                // The first iteration will focus only on the books. 
                var book = new HumanMadeObject()
                    .WithContext()
                    .WithId($"{booksBase}{id}");

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
                    outputAnyway = true;
                }

                // Repeatable. Personal name of a creator/contributor to the Work, plus the role they played. See notes on People below.
                // /created_by/part/carried_out_by/id (for person)
                // /created_by/part/classified_as/id (for role)
                var persAuthors = record.Elements(libNs + "persauthorfull");
                foreach (var author in persAuthors)
                {
                    var persAuthorFull = author.Value.Trim();
                    if (!string.IsNullOrWhiteSpace(persAuthorFull))
                    {
                        if (!persAuthorFullDict.ContainsKey(persAuthorFull))
                        {
                            persAuthorFullDict[persAuthorFull] = [];
                        }
                        persAuthorFullDict[persAuthorFull].Add(id);
                    }
                }


                // corpauthor - Repeatable. Organization/Corporate name of a creator/contributor to the Work. No role provided, assume role=author.
                // See notes on Groups below.
                // /created_by/part/carried_out_by/id
                var corpAuthors = record.Elements(libNs + "corpauthor");
                foreach (var author in corpAuthors)
                {
                    var corpAuthorFull = author.Value;
                    if (!string.IsNullOrWhiteSpace(corpAuthorFull))
                    {
                        if (!corpAuthorDict.ContainsKey(corpAuthorFull))
                        {
                            corpAuthorDict[corpAuthorFull] = [];
                        }
                        corpAuthorDict[corpAuthorFull].Add(id);
                    }
                }

            }


            // Finished first pass through all records

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();

            Console.WriteLine("allBooks keys: " + allBooks.Keys.Count);
            Console.WriteLine("persauthorfull keys: " + persAuthorFullDict.Keys.Count);
            Console.WriteLine("corpAuthor keys: " + corpAuthorDict.Keys.Count);


            // Create Groups for corpauthor and assert in book record.
            int corpIdMinter = 1;
            foreach (var corpAuthor in corpAuthorDict)
            {
                var group = new Group()
                    .WithId(groupBase + corpIdMinter++)
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
                        Console.WriteLine($"Tidied {datePart} to {tidyDate} for {person.Key}");
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
            // Put the reconciled ID in rather than the local
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
            int personIdMinter = 1;
            foreach (var libraryPerson in normalisedLibraryPeople)
            {
                var person = new Person()
                    .WithId(peopleBase + personIdMinter++)
                    .WithLabel(libraryPerson.Name);

                foreach (var roleBooks in libraryPerson.RolesToBooks)
                {
                    var classifiedAs = GetClassificationForRole(roleBooks.Key);
                    foreach(var id in roleBooks.Value)
                    {
                        var book = allBooks[id];
                        book.CreatedBy ??= new Activity(Types.Creation);
                        book.CreatedBy.Part ??= [];
                        book.CreatedBy.Part.Add(new Activity(Types.Creation)
                        {
                            CarriedOutBy = [person],
                            ClassifiedAs = [classifiedAs]
                        });
                    }
                }
            }

            Console.WriteLine("ROLES===============");
            foreach(var role in Roles)
            {
                Console.WriteLine($"{role.Key}: {role.Value}");
            }



            // once serialised in short form, add context and add reconciled equivalents and serialise as groups/people  
            // add in person dates
            // make use of active string in date field (currently being stripped)
            //Sample(allBooks, 1000);
        }

        private static Dictionary<string, int> Roles = [];

        private static LinkedArtObject GetClassificationForRole(string rawRole)
        {
            // See notes for full list
            // use yul-role_mappings.json
            if (!Roles.ContainsKey(rawRole))
            {
                Roles[rawRole] = 0;
            }
            Roles[rawRole] = Roles[rawRole] + 1;

            switch (rawRole)
            {
                case "editor":
                    return Getty.AatType("Editors", "300025526");
                case "contributor":
                    return Getty.AatType("Contributors", "300403974");
                default:
                    return Getty.AatType("Authorship", "300056110");

                // etc etc
            }
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

        private static void Sample(Dictionary<string, HumanMadeObject> allBooks, int interval)
        {
            int count = 0;
            foreach(var book in allBooks)
            {
                if(count % interval == 0)
                {
                    var generatedJson = JsonSerializer.Serialize(book.Value, options);
                    Console.WriteLine(generatedJson);
                }
                count++;
            }
        }
    }
}
