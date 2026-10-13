using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples
{
    public class BasicPatterns
    {

        [LinkedArtExample("object/spring/1")]
        public static HumanMadeObject Spring_1_Simplest()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/spring/1")
                .WithLabel("Jeanne (Spring) by Manet");

            return spring;
        }


        [LinkedArtExample("object/spring/2")]
        public static HumanMadeObject Spring_2_Classification()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/spring/2")
                .WithLabel("Jeanne (Spring) by Manet")
                .WithClassifiedAs(Aat.Artwork)
                .WithClassifiedAs(Aat.Painting);

            return spring;
        }


        [LinkedArtExample("place/paris/1")]
        public static Place Place_Paris_1()
        {
            var paris = new Place()
                .WithContext()
                .WithId("https://linked.art/example/place/paris/1")
                .WithLabel("Paris")
                .WithClassifiedAs(Aat.City);

            return paris;
        }

        [LinkedArtExample("object/spring/3")]
        public static HumanMadeObject Spring_3_Types_of_Types()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/spring/3")
                .WithLabel("Jeanne (Spring) by Manet");

            spring.ClassifiedAs =
            [
                Aat.Painting.WithClassifiedAs(Aat.TypeOfWork),
                Aat.Artwork
            ];

            return spring;
        }


        [LinkedArtExample("person/rembrandt/1")]
        public static Person Person_Rembrandt_1()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId("https://linked.art/example/person/rembrandt/1")
                .WithLabel("Rembrandt")
                .WithClassifiedAs(Aat.NationalityTerm("300111175", "Dutch"));

            return rembrandt;
        }


        [LinkedArtExample("object/nightwatch/1")]
        public static HumanMadeObject NightWatch_1_Names()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/1")
                .WithLabel("Night Watch by Rembrandt");

            var englishName = new Name("The Night Watch")
                .AsPrimaryName()
                .WithLanguage("300388277", "English", "en");
            var dutchName = new Name("De Nachtwacht")
                .AsPrimaryName()
                .WithLanguage("300388256", "Dutch", "nl");

            nightWatch.IdentifiedBy = [englishName, dutchName];

            return nightWatch;
        }


        [LinkedArtExample("object/nightwatch/2")]
        public static HumanMadeObject NightWatch_2_Accession()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/2")
                .WithLabel("Night Watch by Rembrandt");

            var accessionNumber = new Identifier("SK-C-5").AsAccessionNumber();
            var name = new Name("The Night Watch").AsPrimaryName();

            nightWatch.IdentifiedBy = [accessionNumber, name];

            return nightWatch;
        }


        [LinkedArtExample("object/nightwatch/3")]
        public static HumanMadeObject NightWatch_3_Equivalent()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/3")
                .WithLabel("Night Watch by Rembrandt");

            nightWatch.Equivalent = [
                new HumanMadeObject()
                    .WithId("https://www.wikidata.org/entity/Q219831")
                    .WithLabel("Night Watch")
            ];

            return nightWatch;
        }


        [LinkedArtExample("object/nightwatch/4")]
        public static HumanMadeObject NightWatch_4_Statements()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/4")
                .WithLabel("Night Watch by Rembrandt");

            var materials = new LinguisticObject()
                .WithClassifiedAs(
                    Aat.MaterialsStatement,
                    Aat.Statement)
                .WithContent("Oil on Canvas")
                .WithLanguage("300388277", "English", "en");
            
            nightWatch.ReferredToBy = [materials];

            return nightWatch;
        }


        [LinkedArtExample("object/spring/4")]
        public static HumanMadeObject Spring_4_Production()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/spring/4")
                .WithLabel("Jeanne (Spring) by Manet");


            spring.ProducedBy =
                new Production()
                {
                    // https://linked.art/example/object/spring/4.json has no Z timespan
                    TimeSpan = LinkedArtTimeSpan.FromYear(1881).WithLabel("1881"),
                    TookPlaceAt = [
                        new Place()
                            .WithId("https://linked.art/example/place/france")
                            .WithLabel("France")
                    ],
                    CarriedOutBy = [
                        new Person()
                            .WithId("https://linked.art/example/person/manet")
                            .WithLabel("Manet")
                    ],
                    During = [
                        new Period()
                            .WithId("https://linked.art/example/event/19c")
                            .WithLabel("19th Century")
                    ]
                };

            return spring;
        }

        [LinkedArtExample("event/stowe/1")]
        public static Activity Stowe_Auction_1()
        {
            var stowe = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/event/stowe/1")
                .WithLabel("Auction of Stowe House")
                .WithClassifiedAs(Aat.AuctionEvent);

            var timespan = new LinkedArtTimeSpan().WithDaysDimension(3);
            timespan.BeginOfTheBegin = new LinkedArtDate(1848, 8, 1);
            timespan.EndOfTheBegin = new LinkedArtDate(1848, 8, 20).LastSecondOfDay();
            timespan.BeginOfTheEnd = new LinkedArtDate(1848, 9, 9);
            timespan.EndOfTheEnd = new LinkedArtDate(1848, 9, 30).LastSecondOfDay();
            timespan.IdentifiedBy = [
                new Name("40 days in August and September, 1848")
            ];

            stowe.TimeSpan = timespan;

            return stowe;
        }

        [LinkedArtExample("object/spring/support")]
        public static HumanMadeObject Spring_Parts_Support()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/spring/support")
                .WithLabel("Support of Spring")
                .WithClassifiedAs(
                    Aat.Support,
                    Aat.PartType);


            var materials = new LinguisticObject()
                .WithClassifiedAs(
                    Aat.MaterialsStatement,
                    Aat.Statement)
                .WithContent("Canvas");

            spring.ReferredToBy = [materials];

            spring.PartOf = [
                new HumanMadeObject()
                    .WithId("https://linked.art/example/object/spring")
                    .WithLabel("Jeanne (Spring) by Manet")
            ];

            return spring;

        }


        [LinkedArtExample("person/rembrandt/2")]
        public static Person Person_Rembrandt_2_Member_Of()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId("https://linked.art/example/person/rembrandt/2")
                .WithLabel("Rembrandt");

            rembrandt.MemberOf = [
                new Group()
                    .WithId("https://linked.art/example/group/stluke")
                    .WithLabel("Guild of St Luke")
            ];

            return rembrandt;
        }


        [LinkedArtExample("text/rembrandtjaccuse/1")]
        public static LinguisticObject Jaccuse_Parts()
        {
            var jaccuse = new LinguisticObject()
                .WithContext()
                .WithId("https://linked.art/example/text/rembrandtjaccuse/1")
                .WithLabel("Rembrandt's J'accuse");

            jaccuse.CreatedBy = new Creation();
            jaccuse.CreatedBy.TimeSpan = LinkedArtTimeSpan.FromYear(2008).WithLabel("2008");

            var directorActivity = new Creation()
                .WithClassifiedAs(Aat.Term("300025654", "Director"));
            directorActivity.CarriedOutBy = [
                new Person().WithId("https://linked.art/example/person/greenaway").WithLabel("Peter Greenaway")
            ];
            var producerActivity = new Creation()
                .WithClassifiedAs(Aat.Term("300197742", "Producer"));
            producerActivity.CarriedOutBy = [
                new Person().WithId("https://linked.art/example/person/wolting").WithLabel("Femke Wolting")
            ];
            jaccuse.CreatedBy.Part = [
                directorActivity,
                producerActivity
            ];

            return jaccuse;
        }
    }
}
