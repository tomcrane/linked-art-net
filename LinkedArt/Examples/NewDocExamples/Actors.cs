

using LinkedArtNet;
using LinkedArtNet.Vocabulary;

namespace Examples.NewDocExamples
{
    public class Actors
    {
        // https://linked.art/model/actor/
        public static void Create()
        {
            Rembrandt_Guild();
            Rembrandt_Name();
            Rembrandt_Name_Parts();
            Rembrandt_Identifier();
            Rembrandt_Equivalent();
            Rembrandt_Address();
            Rembrandt_Museum();
            Rembrandt_Residence();
            Rembrandt_Life();
            Rembrandt_Museum_Formed();
            Rembrandt_Carried_Out();
            Rembrandt_Participated_In();
            Rembrandt_Biography();
            Rembrandt_Was_Dutch();
            Wiley_Is_African_American();
            Rembrandt_Was_Male();
        }


        private static void Rembrandt_Guild()
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


        private static void Rembrandt_Name()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/person/rembrandt/3")
                .WithLabel("Rembrandt");

            rembrandt.IdentifiedBy = [new Name("Rembrandt Harmenzoon van Rijn").AsPrimaryName()];

            Documentation.Save(rembrandt);
        }


        private static void Rembrandt_Name_Parts()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/person/rembrandt/4")
                .WithLabel("Rembrandt");

            var name = new Name("Rembrandt Harmenzoon van Rijn").AsPrimaryName();
            name.Part = [
                new Name("Rembrandt").WithClassifiedAs(Getty.GivenName),
                new Name()
                    .WithId($"{Documentation.IdRoot}/Name/Harmenzoon")
                    .WithClassifiedAs(Getty.MiddleName),
                new Name("van Rijn").WithClassifiedAs(Getty.FamilyName)
            ];
            rembrandt.IdentifiedBy = [name];

            Documentation.Save(rembrandt);
        }


        private static void Rembrandt_Identifier()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/person/rembrandt/5")
                .WithLabel("Rembrandt");

            rembrandt.IdentifiedBy = [
                new Identifier("Q5598")
                    .WithClassifiedAs(Getty.AatType("Owner-Assigned Number", "300404621"))
            ];

            Documentation.Save(rembrandt);
        }


        private static void Rembrandt_Equivalent()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/person/rembrandt/6")
                .WithLabel("Rembrandt");

            rembrandt.Equivalent = [
                new Person()
                    .WithId("http://vocab.getty.edu/ulan/500011051")
                    .WithLabel("Rembrandt")
            ];

            Documentation.Save(rembrandt);
        }


        private static void Rembrandt_Address()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/person/rembrandt/12")
                .WithLabel("Rembrandt");

            rembrandt.ContactPoint = [
                new Identifier("Jodenbreestraat 4, 1011 NK Amsterdam")
                    .WithClassifiedAs(Getty.StreetAddress)
            ];

            Documentation.Save(rembrandt);
        }

        private static void Rembrandt_Museum()
        {
            var museum = new Group()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/group/rembrandthuis/1")
                .WithLabel("Rembrandt House Museum");

            museum.ContactPoint = [
                new Identifier("Jodenbreestraat 4, 1011 NK Amsterdam")
                    .WithClassifiedAs(Getty.StreetAddress),
                new Identifier("+31-20-520-0400")
                    .WithClassifiedAs(Getty.TelephoneNumber),
                new Identifier("museum@rembrandthuis.nl")
                    .WithClassifiedAs(Getty.EmailAddress)
            ];

            Documentation.Save(museum);
        }


        private static void Rembrandt_Residence()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/person/rembrandt/11")
                .WithLabel("Rembrandt");

            rembrandt.Residence = [
                new Place()
                    .WithId($"{Documentation.IdRoot}/place/rembrandthuis")
                    .WithLabel("Rembrandt's House Place")
            ];

            Documentation.Save(rembrandt);
        }


        private static void Rembrandt_Life()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/person/rembrandt/13")
                .WithLabel("Rembrandt");

            rembrandt.Born = new Activity(Types.Birth)
            {
                TimeSpan = LinkedArtTimeSpan.FromDay(1606, 7, 15)
            };
            rembrandt.Died = new Activity(Types.Death)
            {
                TimeSpan = LinkedArtTimeSpan.FromDay(1669, 10, 4),
                TookPlaceAt = [
                    new Place()
                        .WithId("http://vocab.getty.edu/tgn/7006952")
                        .WithLabel("Amsterdam")
                ]
            };

            // site examples don't have labels - maybe make that a flag?
            rembrandt.Born.TimeSpan.Label = null;
            rembrandt.Died.TimeSpan.Label = null;

            Documentation.Save(rembrandt);
        }


        private static void Rembrandt_Museum_Formed()
        {
            var museum = new Group()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/group/rembrandthuis/2")
                .WithLabel("Rembrandt House Museum");

            museum.FormedBy = new Activity(Types.Formation)
            {
                TimeSpan = LinkedArtTimeSpan.FromDay(1911, 6, 10)
            };

            museum.FormedBy.TimeSpan.Label = null;  

            Documentation.Save(museum);
        }


        private static void Rembrandt_Carried_Out()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/person/rembrandt/7")
                .WithLabel("Rembrandt");

            var activity = new Activity()
                .WithClassifiedAs(Getty.AatType("Professional Activities", "300393177"))
                .WithClassifiedAs(Getty.AatType("Painter", "300025136"));
            activity.TimeSpan = new LinkedArtTimeSpan
            {
                BeginOfTheBegin = new LinkedArtDate(1631, 1, 1),
                EndOfTheEnd = new LinkedArtDate(1669, 10, 4).LastSecondOfDay()
            };
            rembrandt.CarriedOut = [activity];

            Documentation.Save(rembrandt);
        }


        private static void Rembrandt_Participated_In()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/person/rembrandt/14")
                .WithLabel("Rembrandt");

            var activity = new Activity()
                .WithClassifiedAs(Getty.AatType("Burial", "300263485"));
            activity.TimeSpan = new LinkedArtTimeSpan
            {
                BeginOfTheBegin = new LinkedArtDate(1669, 10, 4),
                EndOfTheEnd = new LinkedArtDate(1669, 11, 1).LastSecondOfDay()
            };
            activity.TookPlaceAt = [
                new Place()
                    .WithId($"{Documentation.IdRoot}/place/westerkerk")
                    .WithLabel("Place of Westerkerk")
            ];
            rembrandt.ParticipatedIn = [activity];

            Documentation.Save(rembrandt);
        }


        private static void Rembrandt_Biography()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/person/rembrandt/8")
                .WithLabel("Rembrandt");

            rembrandt.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Getty.BiographyStatement, Getty.BriefText)
                    .WithContent("Rembrandt's work is characterized by the Baroque interest in dramatic scenes and strong contrasts of light on a dark stage")
            ];

            Documentation.Save(rembrandt);
        }



        private static void Rembrandt_Was_Dutch()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/person/rembrandt/9")
                .WithLabel("Rembrandt")
                .WithClassifiedAs(Getty.AatType("Dutch", "300111175"), Getty.Nationality);

            Documentation.Save(rembrandt);
        }


        private static void Wiley_Is_African_American()
        {
            var wiley = new Person()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/person/wiley/1")
                .WithLabel("Kehinde Wiley")
                .WithClassifiedAs("http://www.wikidata.org/entity/Q3007177", "African-American", Getty.Ethnicity);

            Documentation.Save(wiley);
        }


        private static void Rembrandt_Was_Male()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/person/rembrandt/10")
                .WithLabel("Rembrandt")
                .WithClassifiedAs(Getty.AatType("Male", "300189559"), Getty.Gender);

            Documentation.Save(rembrandt, false);
        }

    }
}
