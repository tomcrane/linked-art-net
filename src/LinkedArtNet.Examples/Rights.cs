
using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples
{
    public class Rights
    {
        // https://linked.art/model/object/rights/

        [LinkedArtExample("object/nightwatch/15")]
        public static HumanMadeObject Nightwatch_Credit_Statement()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/15")
                .WithLabel("Night Watch by Rembrandt");


            nightWatch.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Aat.CreditLine, Aat.Statement)
                    .WithContent("On loan from the City of Amsterdam")
            ];

            return nightWatch;
        }


        [LinkedArtExample("visual/nightwatch/1")]
        public static VisualItem Nightwatch_Rights_Statement()
        {
            var nightwatchVisual = new VisualItem()
                .WithContext()
                .WithId("https://linked.art/example/visual/nightwatch/1")
                .WithLabel("Visual Content of Night Watch");
            
            nightwatchVisual.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Aat.RightsStatement, Aat.Statement)
                    .WithContent("Public Domain")
            ];

            return nightwatchVisual;
        }



        [LinkedArtExample("visual/nightwatch/2")]
        public static VisualItem Nightwatch_Rights_PublicDomain()
        {
            var nightwatchVisual = new VisualItem()
                .WithContext()
                .WithId("https://linked.art/example/visual/nightwatch/2")
                .WithLabel("Visual Content of Night Watch");

            var publicDomain = new Right()
                .WithLabel("Night Watch's Public Domain status")
                .WithClassifiedAs(RightsStatements.CreativeCommonsPublicDomain);

            publicDomain.IdentifiedBy = [
                new Name("Public Domain")
            ];

            nightwatchVisual.SubjectTo = [publicDomain];

            return nightwatchVisual;
        }
    }
}
