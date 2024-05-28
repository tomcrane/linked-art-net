

using LinkedArtNet;
using LinkedArtNet.Vocabulary;

namespace Examples.NewDocExamples
{
    public class Actors
    {    
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
                new LinkedArtObject(Types.Identifier)
                    .WithContent("Q5598")
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
                new LinkedArtObject(Types.Identifier)
                    .WithContent("Jodenbreestraat 4, 1011 NK Amsterdam")
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
                new LinkedArtObject(Types.Identifier)
                    .WithContent("Jodenbreestraat 4, 1011 NK Amsterdam")
                    .WithClassifiedAs(Getty.StreetAddress),
                new LinkedArtObject(Types.Identifier)
                    .WithContent("+31-20-520-0400")
                    .WithClassifiedAs(Getty.TelephoneNumber),
                new LinkedArtObject(Types.Identifier)
                    .WithContent("museum@rembrandthuis.nl")
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
    }
}
