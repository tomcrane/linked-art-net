

using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples
{
    public class Exhibitions
    {
        // https://linked.art/model/exhibition/


        [LinkedArtExample("activity/exha/1")]
        public static Activity Manet_and_Modern_Beauty()
        {
            var activity = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/activity/exha/1")
                .WithLabel("Manet and Modern Beauty (Getty)")
                .WithClassifiedAs(Aat.Term("300054766", "Exhibiting"));

            activity.IdentifiedBy = [ new Name("Manet and Modern Beauty").AsPrimaryName() ];

            activity.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Aat.Description, Aat.Statement)
                    .WithContent("The great painter of modern Paris Edouard Manet famously shocked contemporary audiences with his provocative pictures. The first exhibition ever to explore the last years of his short life, Manet and Modern Beauty highlights a less familiar and more intimate side of this celebrated artist's work.")
            ];

            activity.TimeSpan = new LinkedArtTimeSpan()
            {
                BeginOfTheBegin = new LinkedArtDate(2019, 10, 8),
                EndOfTheEnd = new LinkedArtDate(2020, 1, 12).EndOfDay()
            };

            activity.TookPlaceAt = [
                Tgn.Place("7023900", "Los Angeles")
                    .WithClassifiedAs(Aat.City)
            ];

            activity.CarriedOutBy = [
                Ulan.Group("500115988", "Getty Museum")
                    .WithClassifiedAs(Aat.Museum)
            ];

            activity.UsedSpecificObject = [
                new Set()
                    .WithId("https://linked.art/example/set/exhset")
                    .WithLabel("Exhibition objects") 
            ];
            activity.InfluencedBy = [
                new PropositionalObject()
                    .WithId("https://linked.art/example/concept/exhidea")
                    .WithLabel("Idea for Manet and Modern Beauty")
            ];

            return activity;
        }


        [LinkedArtExample("concept/exhidea/1")]
        public static PropositionalObject Exhibition_Concept()
        {
            var concept = new PropositionalObject()
                .WithContext()
                .WithId("https://linked.art/example/concept/exhidea/1")
                .WithLabel("Idea for Manet and Modern Beauty")
                .WithClassifiedAs(Aat.Exhibition);

            concept.IdentifiedBy = [new Name("Manet and Modern Beauty").AsPrimaryName()];

            concept.About = [
                Aat.Term("300055821", "Beauty"),
                Ulan.Person("500010363", "Manet")
            ];

            concept.CreatedBy = new Creation()
            {
                CarriedOutBy = [
                    new Person()
                        .WithId("https://linked.art/example/person/allan")
                        .WithLabel("Scott Allan"),
                    new Person()
                        .WithId("https://linked.art/example/person/beeny")
                        .WithLabel("Emily Beeny"),
                    new Person()
                        .WithId("https://linked.art/example/person/groom")
                        .WithLabel("Gloria Groom"),
                ]
            };

            return concept;
        }

        [LinkedArtExample("concept/exhidea/2")]
        public static PropositionalObject Exhibition_Influenced_By()
        {
            var concept = new PropositionalObject()
                .WithContext()
                .WithId("https://linked.art/example/concept/exhidea/2")
                .WithLabel("Idea for Manet and Modern Beauty")
                .WithClassifiedAs(Aat.Exhibition);

            concept.IdentifiedBy = [new Name("Manet and Modern Beauty").AsPrimaryName()];


            concept.CreatedBy = new Creation()
            {
                InfluencedBy = [
                    Ulan.Person("500010363", "Manet")
                ]
            };

            return concept;
        }


        [LinkedArtExample("activity/exhb/1")]
        public static Activity Manet_and_Modern_Beauty_AIC()
        {
            var activity = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/activity/exhb/1")
                .WithLabel("Manet and Modern Beauty (AIC)")
                .WithClassifiedAs(Aat.Term("300054766", "Exhibiting"));

            activity.IdentifiedBy = [new Name("Manet and Modern Beauty").AsPrimaryName()];

            activity.TimeSpan = new LinkedArtTimeSpan()
            {
                BeginOfTheBegin = new LinkedArtDate(2019, 5, 26),
                EndOfTheEnd = new LinkedArtDate(2019, 9, 8).EndOfDay()
            };

            activity.TookPlaceAt = [
                Tgn.Place("7013596", "Chicago")
                    .WithClassifiedAs(Aat.City)
            ];

            activity.CarriedOutBy = [
                Ulan.Group("500304669", "Art Institute")
                    .WithClassifiedAs(Aat.Museum)
            ];

            activity.UsedSpecificObject = [
                new Set()
                    .WithId("https://linked.art/example/set/exhset")
                    .WithLabel("Exhibition objects")
            ];
            activity.InfluencedBy = [
                new PropositionalObject()
                    .WithId("https://linked.art/example/concept/exhidea")
                    .WithLabel("Idea for Manet and Modern Beauty")
            ];

            activity.PartOf = [
                new Activity()
                    .WithId("https://linked.art/example/event/exhab")
                    .WithLabel("Manet and Modern Beauty")
            ];

            return activity;
        }


        [LinkedArtExample("event/exhab/1")]
        public static Activity Multiple_Locations()
        {
            var activity = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/event/exhab/1")
                .WithLabel("Manet and Modern Beauty")
                .WithClassifiedAs(Aat.ExhibitingInMultipleLocations);

            activity.IdentifiedBy = [new Name("Manet and Modern Beauty").AsPrimaryName()];

            activity.TimeSpan = new LinkedArtTimeSpan()
            {
                BeginOfTheBegin = new LinkedArtDate(2019, 5, 26),
                EndOfTheEnd = new LinkedArtDate(2020, 1, 12).EndOfDay()
            };
            activity.InfluencedBy = [
                new PropositionalObject()
                    .WithId("https://linked.art/example/concept/exhidea")
                    .WithLabel("Idea for Manet and Modern Beauty")
            ];

            return activity;
        }


        [LinkedArtExample("set/exhset/1")]
        public static Set Exhibition_Objects()
        {
            var set = new Set()
                .WithContext()
                .WithId("https://linked.art/example/set/exhset/1")
                .WithLabel("Exhibition objects")
                .WithClassifiedAs(Aat.Term("300378926", "Exhibition Collection"));

            set.IdentifiedBy = [new Name("Objects in Manet and Modern Beauty").AsPrimaryName()];

            set.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Aat.Description, Aat.Statement)
                    .WithContent("Objects in the exhibition Manet and Modern Beauty at the Art Institute of Chicago and the Getty Museum")
            ];

            set.CreatedBy = new Creation()
            {
                TimeSpan = new LinkedArtTimeSpan
                {
                    BeginOfTheBegin = new LinkedArtDate(2019, 5, 1),
                    EndOfTheEnd = new LinkedArtDate(2019, 5, 1).EndOfDay()
                }
            };

            return set;
        }


        [LinkedArtExample("object/spring/12")]
        public static HumanMadeObject Spring_in_Exhibition()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/spring/12")
                .WithLabel("Jeanne (Spring) by Manet")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

            spring.IdentifiedBy = [new Name("Jeanne (Spring)")];

            spring.MemberOf = [
                new Set()
                    .WithId("https://linked.art/example/set/exhset")
            ];

            return spring;
        }
    }
}
