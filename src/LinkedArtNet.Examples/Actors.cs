

using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples
{
    public class Actors
    {
        // https://linked.art/model/actor/


        [LinkedArtExample("person/rembrandt/3")]
        public static Person Rembrandt_Name()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId("https://linked.art/example/person/rembrandt/3")
                .WithLabel("Rembrandt");

            rembrandt.IdentifiedBy = [new Name("Rembrandt Harmenzoon van Rijn").AsPrimaryName()];

            return rembrandt;
        }

        [LinkedArtExample("person/rembrandt/4")]
        public static Person Rembrandt_Name_Parts()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId("https://linked.art/example/person/rembrandt/4")
                .WithLabel("Rembrandt");

            var name = new Name("Rembrandt Harmenzoon van Rijn").AsPrimaryName();
            name.Part = [
                new Name("Rembrandt").WithClassifiedAs(Aat.GivenName),
                new Name()
                    .WithId("https://linked.art/example/Name/Harmenzoon")
                    .WithClassifiedAs(Aat.MiddleName),
                new Name("van Rijn").WithClassifiedAs(Aat.FamilyName)
            ];
            rembrandt.IdentifiedBy = [name];

            return rembrandt;
        }


        [LinkedArtExample("person/rembrandt/6")]
        public static Person Rembrandt_Equivalent()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId("https://linked.art/example/person/rembrandt/6")
                .WithLabel("Rembrandt");

            rembrandt.Equivalent = [
                Ulan.Person("500011051", "Rembrandt")
            ];

            return rembrandt;
        }




        [LinkedArtExample("person/rembrandt/12")]
        public static Person Rembrandt_Address()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId("https://linked.art/example/person/rembrandt/12")
                .WithLabel("Rembrandt");

            rembrandt.ContactPoint = [
                new Identifier("Jodenbreestraat 4, 1011 NK Amsterdam")
                    .WithClassifiedAs(Aat.StreetAddress)
            ];

            return rembrandt;
        }





        [LinkedArtExample("group/rembrandthuis/1")]
        public static Group Rembrandt_Museum()
        {
            var museum = new Group()
                .WithContext()
                .WithId("https://linked.art/example/group/rembrandthuis/1")
                .WithLabel("Rembrandt House Museum");

            museum.ContactPoint = [
                new Identifier("Jodenbreestraat 4, 1011 NK Amsterdam")
                    .WithClassifiedAs(Aat.StreetAddress),
                new Identifier("+31-20-520-0400")
                    .WithClassifiedAs(Aat.TelephoneNumber),
                new Identifier("museum@rembrandthuis.nl")
                    .WithClassifiedAs(Aat.EmailAddress)
            ];

            return museum;
        }


        [LinkedArtExample("person/rembrandt/11")]
        public static Person Rembrandt_Residence()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId("https://linked.art/example/person/rembrandt/11")
                .WithLabel("Rembrandt");

            rembrandt.Residence = [
                new Place()
                    .WithId("https://linked.art/example/place/rembrandthuis")
                    .WithLabel("Rembrandt's House Place")
            ];

            return rembrandt;
        }


        [LinkedArtExample("person/rembrandt/13")]
        public static Person Rembrandt_Life()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId("https://linked.art/example/person/rembrandt/13")
                .WithLabel("Rembrandt");

            rembrandt.Born = new Birth()
            {
                TimeSpan = LinkedArtTimeSpan.FromDay(1606, 7, 15)
            };
            rembrandt.Died = new Death()
            {
                TimeSpan = LinkedArtTimeSpan.FromDay(1669, 10, 4),
                TookPlaceAt = [
                    Tgn.Place("7006952", "Amsterdam")
                ]
            };


            return rembrandt;
        }


        [LinkedArtExample("group/rembrandthuis/2")]
        public static Group Rembrandt_Museum_Formed()
        {
            var museum = new Group()
                .WithContext()
                .WithId("https://linked.art/example/group/rembrandthuis/2")
                .WithLabel("Rembrandt House Museum");

            museum.FormedBy = new Formation()
            {
                TimeSpan = LinkedArtTimeSpan.FromDay(1911, 6, 10)
            };


            return museum;
        }


        [LinkedArtExample("person/rembrandt/7")]
        public static Person Rembrandt_Carried_Out()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId("https://linked.art/example/person/rembrandt/7")
                .WithLabel("Rembrandt");

            var activity = new Activity()
                .WithClassifiedAs(Aat.ProfessionalActivity)
                .WithClassifiedAs(Aat.Term("300025136", "Painter"));
            activity.TimeSpan = new LinkedArtTimeSpan
            {
                BeginOfTheBegin = new LinkedArtDate(1631, 1, 1),
                EndOfTheEnd = new LinkedArtDate(1669, 10, 4).LastSecondOfDay()
            };
            rembrandt.CarriedOut = [activity];

            return rembrandt;
        }


        [LinkedArtExample("person/rembrandt/14")]
        public static Person Rembrandt_Participated_In()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId("https://linked.art/example/person/rembrandt/14")
                .WithLabel("Rembrandt");

            var activity = new Activity()
                .WithClassifiedAs(Aat.Term("300263485", "Burial"));
            activity.TimeSpan = new LinkedArtTimeSpan
            {
                BeginOfTheBegin = new LinkedArtDate(1669, 10, 4),
                EndOfTheEnd = new LinkedArtDate(1669, 11, 1).LastSecondOfDay()
            };
            activity.TookPlaceAt = [
                new Place()
                    .WithId("https://linked.art/example/place/westerkerk")
                    .WithLabel("Place of Westerkerk")
            ];
            rembrandt.ParticipatedIn = [activity];

            return rembrandt;
        }


        [LinkedArtExample("person/rembrandt/8")]
        public static Person Rembrandt_Biography()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId("https://linked.art/example/person/rembrandt/8")
                .WithLabel("Rembrandt");

            rembrandt.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Aat.BiographyStatement, Aat.Statement)
                    .WithContent("Rembrandt's work is characterized by the Baroque interest in dramatic scenes and strong contrasts of light on a dark stage")
            ];

            return rembrandt;
        }



        [LinkedArtExample("person/rembrandt/9")]
        public static Person Rembrandt_Was_Dutch()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId("https://linked.art/example/person/rembrandt/9")
                .WithLabel("Rembrandt")
                .WithClassifiedAs(Aat.NationalityTerm("300111175", "Dutch"));

            return rembrandt;
        }


        [LinkedArtExample("person/wiley/1")]
        public static Person Wiley_Is_African_American()
        {
            var wiley = new Person()
                .WithContext()
                .WithId("https://linked.art/example/person/wiley/1")
                .WithLabel("Kehinde Wiley")
                .WithClassifiedAs("http://www.wikidata.org/entity/Q3007177", "African-American", Aat.Ethnicity);

            return wiley;
        }


        [LinkedArtExample("person/rembrandt/10")]
        [SiteIssue("The site publishes two examples as person/rembrandt/10; the JSON at that URL is the assertion page's 'Source of Knowledge' example, not the gender example shown on the actor page.")]
        public static Person Rembrandt_Was_Male()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId("https://linked.art/example/person/rembrandt/10")
                .WithLabel("Rembrandt")
                .WithClassifiedAs(Aat.Term("300189559", "Male"), Aat.Gender);

            return rembrandt;
        }

        [LinkedArtExample("person/rembrandt/31")]
        public static Person Rembrandt_Was_An_Artist()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId("https://linked.art/example/person/rembrandt/31")
                .WithLabel("Rembrandt")
                .WithClassifiedAs(
                    Aat.Artist, 
                        Aat.Occupation);

            return rembrandt;
        }



        //private static void Rembrandt_Identifier()
        //{
        //    var rembrandt = new Person()
        //        .WithContext()
        //        .WithId("https://linked.art/example/person/rembrandt/5")
        //        .WithLabel("Rembrandt");

        //    rembrandt.IdentifiedBy = [
        //        new Identifier("Q5598")
        //            .WithClassifiedAs(Aat.Term("300404621", "Owner-Assigned Number"))
        //    ];

        //    Documentation.Save(rembrandt);
        //}


    }
}
