using LinkedArtNet;
using LinkedArtNet.Vocabulary;

namespace Examples.NewDocExamples
{
    public class BasicPatterns
    {
        public static void Create()
        {
            // https://linked.art/model/base/

            Spring_1_Simplest();
            Spring_2_Classification();
            Place_Paris_1();
            Spring_3_Types_of_Types();
            Person_Rembrandt_1();
            NightWatch_1_Names();
            NightWatch_2_Accession();
            NightWatch_3_Equivalent();
            NightWatch_4_Statements();
            Spring_4_Production();
            Stowe_Auction_1();
            Spring_Parts_Support();
            Person_Rembrandt_2_Member_Of();
            Jaccuse_Parts();
        }


        private static void Spring_1_Simplest()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/spring/1")
                .WithLabel("Jeanne (Spring) by Manet");

            Documentation.Save(spring);
        }


        private static void Spring_2_Classification()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/spring/2")
                .WithLabel("Jeanne (Spring) by Manet")
                .WithClassifiedAs(Getty.Artwork)
                .WithClassifiedAs(Getty.Painting);

            Documentation.Save(spring);
        }


        private static void Place_Paris_1()
        {
            var paris = new Place()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/place/paris/1")
                .WithLabel("Paris")
                .WithClassifiedAs(Getty.AatType("City", "300008389"));

            Documentation.Save(paris);
        }

        private static void Spring_3_Types_of_Types()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/spring/3")
                .WithLabel("Jeanne (Spring) by Manet");

            spring.ClassifiedAs =
            [
                Getty.Painting.WithClassifiedAs(Getty.TypeOfWork),
                Getty.Artwork
            ];

            Documentation.Save(spring);
        }


        private static void Person_Rembrandt_1()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/person/rembrandt/1")
                .WithLabel("Rembrandt")
                .WithClassifiedAs(Getty.Language("300388256", "Dutch"));
            // QUESTION - is this an OK use of a _Language_ when we mean a ...nationality...?
            
            Documentation.Save(rembrandt);
        }


        private static void NightWatch_1_Names()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/nightwatch/1")
                .WithLabel("Night Watch by Rembrandt");

            var englishName = new Name("The Night Watch")
                .AsPrimaryName()
                .WithLanguage("300388277", "English");
            var dutchName = new Name("De Nachtwacht")
                .AsPrimaryName()
                .WithLanguage("300388256", "Dutch");

            nightWatch.IdentifiedBy = [englishName, dutchName];

            Documentation.Save(nightWatch);
        }


        private static void NightWatch_2_Accession()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/nightwatch/2")
                .WithLabel("Night Watch by Rembrandt");

            var accessionNumber =  new LinkedArtObject(Types.Identifier)
                .WithContent("SK-C-5")
                .WithClassifiedAs(Getty.AatType("Accession Number", "300312355"));
            var name = new Name("The Night Watch")
                .AsPrimaryName();

            nightWatch.IdentifiedBy = [accessionNumber, name];

            Documentation.Save(nightWatch);
        }


        private static void NightWatch_3_Equivalent()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/nightwatch/3")
                .WithLabel("Night Watch by Rembrandt");

            nightWatch.Equivalent = [
                new HumanMadeObject()
                    .WithId("https://www.wikidata.org/entity/Q219831")
                    .WithLabel("Night Watch")
            ];

            Documentation.Save(nightWatch);
        }


        private static void NightWatch_4_Statements()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/nightwatch/4")
                .WithLabel("Night Watch by Rembrandt");

            var materials = new LinguisticObject()
                .WithClassifiedAs(
                    Getty.AatType("Material Statement", "300435429"),
                    Getty.AatType("Brief Text", "300418049"))
                .WithContent("Oil on Canvas")
                .WithLanguage("300388277", "English");
            
            nightWatch.ReferredToBy = [materials];

            Documentation.Save(nightWatch);
        }


        private static void Spring_4_Production()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/spring/4")
                .WithLabel("Jeanne (Spring) by Manet");


            spring.ProducedBy =
                new Activity(Types.Production)
                {
                    // https://linked.art/example/object/spring/4.json has no Z timespan
                    TimeSpan = LinkedArtTimeSpan.FromYear(1881),
                    TookPlaceAt = [
                        new Place()
                            .WithId($"{Documentation.IdRoot}/place/france")
                            .WithLabel("France")
                    ],
                    CarriedOutBy = [
                        new Person()
                            .WithId($"{Documentation.IdRoot}/person/manet")
                            .WithLabel("Manet")
                    ]
                };

            Documentation.Save(spring);
        }

        private static void Stowe_Auction_1()
        {
            var stowe = new Activity()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/event/stowe/1")
                .WithLabel("Auction of Stowe House")
                .WithClassifiedAs(Getty.AatType("Auction Event", "300054751"));

            var timespan = new LinkedArtTimeSpan().WithDaysDimension(3);
            timespan.BeginOfTheBegin = new LinkedArtDate(1848, 8, 1);
            timespan.EndOfTheBegin = new LinkedArtDate(1848, 8, 20).LastSecondOfDay();
            timespan.BeginOfTheEnd = new LinkedArtDate(1848, 9, 9);
            timespan.EndOfTheEnd = new LinkedArtDate(1848, 9, 30).LastSecondOfDay();
            timespan.IdentifiedBy = [
                new Name("40 days in August and September, 1848")
            ];

            stowe.TimeSpan = timespan;

            Documentation.Save(stowe);
        }

        private static void Spring_Parts_Support()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/spring/support")
                .WithLabel("Support of Spring")
                .WithClassifiedAs(
                    Getty.AatType("Support", "300014844"),
                    Getty.PartType);


            var materials = new LinguisticObject()
                .WithClassifiedAs(
                    Getty.AatType("Material Statement", "300435429"),
                    Getty.AatType("Brief Text", "300418049"))
                .WithContent("Canvas");

            spring.ReferredToBy = [materials];

            spring.PartOf = [
                new HumanMadeObject()
                    .WithId($"{Documentation.IdRoot}/object/spring")
                    .WithLabel("Jeanne (Spring) by Manet")
            ];

            Documentation.Save(spring);

        }


        private static void Person_Rembrandt_2_Member_Of()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/person/rembrandt/2")
                .WithLabel("Rembrandt");

            rembrandt.MemberOf = [
                new Group()
                    .WithId($"{Documentation.IdRoot}/group/stluke")
                    .WithLabel("Guild of St Luke")
            ];

            Documentation.Save(rembrandt);
        }


        private static void Jaccuse_Parts()
        {
            var jaccuse = new LinguisticObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/text/rembrandtjaccuse/1")
                .WithLabel("Rembrandt's J'accuse");

            jaccuse.CreatedBy = new Activity(Types.Creation);
            jaccuse.CreatedBy.TimeSpan = LinkedArtTimeSpan.FromYear(2008);

            var directorActivity = new Activity(Types.Creation)
                .WithClassifiedAs(Getty.AatType("Director", "300025654"));
            directorActivity.CarriedOutBy = [
                new Person().WithId($"{Documentation.IdRoot}/person/greenaway").WithLabel("Peter Greenaway")
            ];
            var producerActivity = new Activity(Types.Creation)
                .WithClassifiedAs(Getty.AatType("Producer", "300197742"));
            producerActivity.CarriedOutBy = [
                new Person().WithId($"{Documentation.IdRoot}/person/wolting").WithLabel("Femke Wolting")
            ];
            jaccuse.CreatedBy.Part = [
                directorActivity,
                producerActivity
            ];

            Documentation.Save(jaccuse);
        }
    }
}
