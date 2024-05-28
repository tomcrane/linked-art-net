
using LinkedArtNet.Vocabulary;
using LinkedArtNet;

namespace Examples.NewDocExamples
{
    public class Rights
    {
        // https://linked.art/model/object/rights/

        public static void Create()
        {
            Nightwatch_Credit_Statement();
            Nightwatch_Rights_Statement();
            Nightwatch_Rights_PublicDomain();
        }

        private static void Nightwatch_Credit_Statement()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/nightwatch/15")
                .WithLabel("Night Watch by Rembrandt")  
                .WithClassifiedAs(Getty.Painting, Getty.TypeOfWork);


            nightWatch.ReferredToBy = [
                new LinkedArtObject(Types.LinguisticObject)
                    .WithClassifiedAs(Getty.CreditStatement, Getty.BriefText)
                    .WithContent("On loan from the City of Amsterdam")
            ];

            Documentation.Save(nightWatch);
        }


        private static void Nightwatch_Rights_Statement()
        {
            var nightwatchVisual = new Work(Types.VisualItem)
                .WithContext()
                .WithId($"{Documentation.IdRoot}/visual/nightwatch/1")
                .WithLabel("Visual Content of Night Watch");
            
            nightwatchVisual.ReferredToBy = [
                new LinkedArtObject(Types.LinguisticObject)
                    .WithClassifiedAs(Getty.CopyrightLicenseStatement, Getty.BriefText)
                    .WithContent("Public Domain")
            ];

            Documentation.Save(nightwatchVisual);
        }



        private static void Nightwatch_Rights_PublicDomain()
        {
            var nightwatchVisual = new Work(Types.VisualItem)
                .WithContext()
                .WithId($"{Documentation.IdRoot}/visual/nightwatch/2")
                .WithLabel("Visual Content of Night Watch");

            var publicDomain = new Right()
                .WithLabel("Night Watch's Public Domain status")
                .WithClassifiedAs(RightsStatements.CreativeCommonsPublicDomain);

            // move this into Right?
            publicDomain.IdentifiedBy = [
                new Name("Public Domain")
            ];

            nightwatchVisual.SubjectTo = [publicDomain];

            Documentation.Save(nightwatchVisual);
        }
    }
}
