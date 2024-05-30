using LinkedArtNet;
using LinkedArtNet.Vocabulary;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

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

            XNamespace libNs= "x-schema:EF-34074-Export.dtd";

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
                if(!string.IsNullOrWhiteSpace(edition))
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
                foreach(var author in persAuthors)
                {
                    var persAuthorFull = author.Value;
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

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();

            Console.WriteLine("allBooks keys: " + allBooks.Keys.Count);
            Console.WriteLine("persauthorfull keys: " + persAuthorFullDict.Keys.Count);
            Console.WriteLine("corpAuthor keys: " + corpAuthorDict.Keys.Count);

            int corpIdMinter = 1;
            foreach(var corpAuthor in corpAuthorDict)
            {
                var group = new Group()
                    .WithId(groupBase + corpIdMinter++)
                    .WithLabel(corpAuthor.Key);

                foreach(var id in corpAuthor.Value)
                {
                    var book = allBooks[id];
                    book.CreatedBy ??= new Activity(Types.Creation);
                    book.CreatedBy.Part ??= [];
                    book.CreatedBy.Part.Add(new Activity(Types.Creation) {
                        CarriedOutBy = [group]
                    });
                }
            }

            // Now split people into roles and dates and do similar as above.
            // And work out how to reconcile with Getty and LoC.


            // once serialised in short form, add context and add reconciled equivalents and serialise as groups/people  
            Sample(allBooks, 1000);
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
