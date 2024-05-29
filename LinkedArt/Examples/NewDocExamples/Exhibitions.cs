

using LinkedArtNet;
using LinkedArtNet.Vocabulary;

namespace Examples.NewDocExamples
{
    public class Exhibitions
    {
        // https://linked.art/model/exhibition/

        public static void Create()
        {
            Manet_and_Modern_Beauty();
            Exhibition_Concept();
            Exhibition_Influenced_By();
            Manet_and_Modern_Beauty_AIC();
            Multiple_Locations();
            Exhibition_Objects();
            Spring_in_Exhibition();
        }


        private static void Manet_and_Modern_Beauty()
        {
            var activity = new Activity()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/activity/exha/1")
                .WithLabel("Manet and Modern Beauty (Getty)")
                .WithClassifiedAs(Getty.Exhibiting);

            activity.IdentifiedBy = [ new Name("Manet and Modern Beauty").AsPrimaryName() ];

            activity.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Getty.Description, Getty.BriefText)
                    .WithContent("The great painter of modern Paris Edouard Manet famously shocked contemporary audiences with his provocative pictures. The first exhibition ever to explore the last years of his short life, Manet and Modern Beauty highlights a less familiar and more intimate side of this celebrated artist's work.")
            ];

            activity.TimeSpan = new LinkedArtTimeSpan()
            {
                BeginOfTheBegin = new LinkedArtDate(2019, 10, 8),
                EndOfTheEnd = new LinkedArtDate(2020, 1, 12)
            };

            activity.TookPlaceAt = [
                new Place()
                    .WithId("http://vocab.getty.edu/tgn/7023900")
                    .WithLabel("Los Angeles")
                    .WithClassifiedAs(Getty.City)
            ];

            activity.CarriedOutBy = [
                new Group()
                    .WithId("http://vocab.getty.edu/ulan/500115988")
                    .WithLabel("Getty Museum")
                    .WithClassifiedAs(Getty.Museum)
            ];

            activity.UsedSpecificObject = [
                new LinkedArtObject(Types.Set)
                    .WithId($"{Documentation.IdRoot}/set/exhset")
                    .WithLabel("Exhibition objects") 
            ];

            activity.MotivatedBy = [
                new LinkedArtObject(Types.PropositionalObject)
                    .WithId($"{Documentation.IdRoot}/concept/exhidea")
                    .WithLabel("Idea for Manet and Modern Beauty")
            ];

            Documentation.Save(activity, false);
        }


        private static void Exhibition_Concept()
        {
            var concept = new LinkedArtObject(Types.PropositionalObject)
                .WithContext()
                .WithId($"{Documentation.IdRoot}/concept/exhidea/1")
                .WithLabel("Idea for Manet and Modern Beauty")
                .WithClassifiedAs(Getty.Exhibition);

            concept.IdentifiedBy = [new Name("Manet and Modern Beauty").AsPrimaryName()];

            concept.About = [
                Getty.AatType("Beauty", "300055821"),
                new Person().WithId("http://vocab.getty.edu/ulan/500010363").WithLabel("Manet")
            ];

            concept.CreatedBy = new Activity(Types.Creation)
            {
                CarriedOutBy = [
                    new Person()
                        .WithId($"{Documentation.IdRoot}/person/allan")
                        .WithLabel("Scott Allan"),
                    new Person()
                        .WithId($"{Documentation.IdRoot}/person/beeny")
                        .WithLabel("Emily Beeny"),
                    new Person()
                        .WithId($"{Documentation.IdRoot}/person/groom")
                        .WithLabel("Gloria Groom"),
                ]
            };

            Documentation.Save(concept);
        }

        private static void Exhibition_Influenced_By()
        {
            var concept = new LinkedArtObject(Types.PropositionalObject)
                .WithContext()
                .WithId($"{Documentation.IdRoot}/concept/exhidea/2")
                .WithLabel("Idea for Manet and Modern Beauty")
                .WithClassifiedAs(Getty.Exhibition);

            concept.IdentifiedBy = [new Name("Manet and Modern Beauty").AsPrimaryName()];


            concept.CreatedBy = new Activity(Types.Creation)
            {
                InfluencedBy = [
                    new Person()
                        .WithId("http://vocab.getty.edu/ulan/500010363")
                        .WithLabel("Manet")
                ]
            };

            Documentation.Save(concept);
        }


        private static void Manet_and_Modern_Beauty_AIC()
        {
            var activity = new Activity()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/activity/exhb/1")
                .WithLabel("Manet and Modern Beauty (AIC)")
                .WithClassifiedAs(Getty.Exhibiting);

            activity.IdentifiedBy = [new Name("Manet and Modern Beauty").AsPrimaryName()];

            activity.TimeSpan = new LinkedArtTimeSpan()
            {
                BeginOfTheBegin = new LinkedArtDate(2019, 5, 26),
                EndOfTheEnd = new LinkedArtDate(2019, 9, 8)
            };

            activity.TookPlaceAt = [
                new Place()
                    .WithId("http://vocab.getty.edu/tgn/7013596")
                    .WithLabel("Chicago")
                    .WithClassifiedAs(Getty.City)
            ];

            activity.CarriedOutBy = [
                new Group()
                    .WithId("http://vocab.getty.edu/ulan/500304669")
                    .WithLabel("Art Institute")
                    .WithClassifiedAs(Getty.Museum)
            ];

            activity.UsedSpecificObject = [
                new LinkedArtObject(Types.Set)
                    .WithId($"{Documentation.IdRoot}/set/exhset")
                    .WithLabel("Exhibition objects")
            ];

            activity.MotivatedBy = [
                new LinkedArtObject(Types.PropositionalObject)
                    .WithId($"{Documentation.IdRoot}/concept/exhidea")
                    .WithLabel("Idea for Manet and Modern Beauty")
            ];

            activity.PartOf = [
                new Activity()
                    .WithId($"{Documentation.IdRoot}/event/exhab")
                    .WithLabel("Manet and Modern Beauty")
            ];

            Documentation.Save(activity, false);
        }


        private static void Multiple_Locations()
        {
            var activity = new Activity()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/activity/exhab/1")
                .WithLabel("Manet and Modern Beauty")
                .WithClassifiedAs(Getty.AatType("Exhibiting in multiple locations", "300054773"));

            activity.IdentifiedBy = [new Name("Manet and Modern Beauty").AsPrimaryName()];

            activity.TimeSpan = new LinkedArtTimeSpan()
            {
                BeginOfTheBegin = new LinkedArtDate(2019, 5, 26),
                EndOfTheEnd = new LinkedArtDate(2020, 1, 12)
            };

            activity.MotivatedBy = [
                new LinkedArtObject(Types.PropositionalObject)
                    .WithId($"{Documentation.IdRoot}/concept/exhidea")
                    .WithLabel("Idea for Manet and Modern Beauty")
            ];

            Documentation.Save(activity, false);
        }


        private static void Exhibition_Objects()
        {
            var set = new LinkedArtObject(Types.Set)
                .WithContext()
                .WithId($"{Documentation.IdRoot}/set/exset/1")
                .WithLabel("Exhibition objects")
                .WithClassifiedAs(Getty.AatType("Exhibition Collection", "300378926"));

            set.IdentifiedBy = [new Name("Objects in Manet and Modern Beauty").AsPrimaryName()];

            set.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Getty.Description, Getty.BriefText)
                    .WithContent("Objects in the exhibition Manet and Modern Beauty at the Art Institute of Chicago and the Getty Museum")
            ];

            set.CreatedBy = new Activity(Types.Creation)
            {
                TimeSpan = new LinkedArtTimeSpan
                {
                    BeginOfTheBegin = new LinkedArtDate(2019, 5, 1),
                    EndOfTheEnd = new LinkedArtDate(2019, 5, 1)
                }
            };

            Documentation.Save(set, false);
        }


        private static void Spring_in_Exhibition()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/spring/12")
                .WithLabel("Jeanne (Spring) by Manet")
                .WithClassifiedAs(Getty.Painting, Getty.TypeOfWork);

            spring.IdentifiedBy = [new Name("Jeanne (Spring)")];

            spring.MemberOf = [
                new LinkedArtObject(Types.Set)
                    .WithId($"{Documentation.IdRoot}/set/exhset")
            ];

            Documentation.Save (spring);
        }
    }
}
