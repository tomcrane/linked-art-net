using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples
{
    /// <summary>https://linked.art/model/concept/</summary>
    public class Concepts
    {
        [LinkedArtExample("concept/painting/1")]
        public static LinkedArtNet.Type Painting_Concept()
        {
            var painting = new LinkedArtNet.Type()
                .WithContext()
                .WithId("https://linked.art/example/concept/painting/1")
                .WithLabel("Painting");

            return painting;
        }


        [LinkedArtExample("concept/painting/2")]
        public static LinkedArtNet.Type Local_Concept_Equivalent_To_AAT()
        {
            var painting = new LinkedArtNet.Type()
                .WithContext()
                .WithId("https://linked.art/example/concept/painting/2")
                .WithLabel("Local Copy of Painting");

            painting.IdentifiedBy = [new Name("Painting").AsPrimaryName()];
            painting.Equivalent = [Aat.Term("300033618", "painting (visual works)")];

            return painting;
        }


        [LinkedArtExample("concept/painting/3")]
        public static LinkedArtNet.Type Local_Concept_With_Broader()
        {
            var painting = new LinkedArtNet.Type()
                .WithContext()
                .WithId("https://linked.art/example/concept/painting/3")
                .WithLabel("Local Copy of Painting")
                .WithClassifiedAs(Aat.Term("300435443", "type of work"));

            painting.Equivalent = [Aat.Term("300033618", "painting (visual works)")];
            painting.Broader = [Aat.Term("300191086", "visual works")];

            return painting;
        }


        [LinkedArtExample("concept/painting/4")]
        public static LinkedArtNet.Type Local_Concept_In_Scheme()
        {
            var painting = new LinkedArtNet.Type()
                .WithContext()
                .WithId("https://linked.art/example/concept/painting/4")
                .WithLabel("Local Copy of Painting");

            painting.MemberOf = [
                new Set()
                    .WithId("https://linked.art/example/set/conceptScheme")
                    .WithLabel("Local Concept Scheme")
            ];

            return painting;
        }


        [LinkedArtExample("concept/historyFrance/1")]
        public static LinkedArtNet.Type Concept_Created_From_Others()
        {
            var historyOfFrance = new LinkedArtNet.Type()
                .WithContext()
                .WithId("https://linked.art/example/concept/historyFrance/1")
                .WithLabel("History of France");

            historyOfFrance.CreatedBy = new Creation
            {
                InfluencedBy = [
                    new LinkedArtNet.Type()
                        .WithId("https://linked.art/example/concept/history")
                        .WithLabel("History"),
                    new Place()
                        .WithId("https://linked.art/example/place/france")
                        .WithLabel("France")
                ]
            };

            return historyOfFrance;
        }


        [LinkedArtExample("object/nightwatch/52")]
        public static HumanMadeObject NightWatch_Classified_With_Local_Concept()
        {
            var nightwatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/52")
                .WithLabel("Night Watch by Rembrandt");

            nightwatch.ClassifiedAs = [
                new LinkedArtNet.Type()
                    .WithId("https://linked.art/example/concept/painting")
                    .WithLabel("Local Copy of Painting")
                    .WithEquivalent(Aat.Term("300033618", "painting (visual art)"))
            ];

            return nightwatch;
        }
    }
}
