using LinkedArtNet.Vocabulary;
using LinkedArtNet;

namespace Examples.NewDocExamples
{
    public class Aboutness
    {
        // https://linked.art/model/object/aboutness/

        public static void Create()
        {
            Spring_5_Description();
            Spring_Visual_Content();
            Spring_Respresents_Instance_of_Type();
            Spring_About();
            Spring_Style();
            Spring_Portrait();
            Nightwatch_Signature();
            Exhibition_Catalog_1();
            Harpers_Poster();
            Harpers_Visual_Content();
            Harpers_Textual_Content();
        }


        private static void Spring_5_Description()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/spring/5")
                .WithLabel("Jeanne (Spring) by Manet");


            spring.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Aat.Description, Aat.BriefText)
                    .WithContent("A chic young woman in a day dress with floral accents holds a parasol against a background of exuberant foliage.")
            ];

            spring.Shows = [
                new VisualItem()
                    .WithId($"{Documentation.IdRoot}/visual/spring")
                    .WithLabel("Visual Content of Spring")
            ];

            Documentation.Save(spring);
        }


        private static void Spring_Visual_Content()
        {
            var springVis = new VisualItem()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/visual/spring/1")
                .WithLabel("Visual Content of Spring");

            springVis.Represents = [
                new Person()
                    .WithId($"{Documentation.IdRoot}/person/jeanne")
                    .WithLabel("Jeanne Demarsy")
            ];

            Documentation.Save(springVis);
        }


        private static void Spring_Respresents_Instance_of_Type()
        {
            var springVis = new VisualItem()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/visual/spring/5")
                .WithLabel("Visual Content of Spring");

            springVis.RepresentsInstanceOfType = [
                Aat.Parasol
            ];

            Documentation.Save(springVis);
        }


        private static void Spring_About()
        {
            var springVis = new VisualItem()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/visual/spring/2")
                .WithLabel("Visual Content of Spring");

            springVis.About = [
                Aat.Spring
            ];

            Documentation.Save(springVis);
        }

        private static void Spring_Style()
        {
            var springVis = new VisualItem()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/visual/spring/3")
                .WithLabel("Visual Content of Spring")
                .WithClassifiedAs(Aat.Impressionism, Aat.Style);

            Documentation.Save(springVis);
        }


        private static void Spring_Portrait()
        {
            var springVis = new VisualItem()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/visual/spring/4")
                .WithLabel("Visual Content of Spring")
                .WithClassifiedAs(Aat.Portrait);

            Documentation.Save(springVis);
        }


        private static void Nightwatch_Signature()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/nightwatch/18");

            nightWatch.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Aat.Signature, Aat.BriefText)
                    .WithContent("signature and date: ‘Rembrandt f 1642’")
            ];

            Documentation.Save(nightWatch);
        }


        private static void Exhibition_Catalog_1()
        {
            var catalog = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/catalog/1")
                .WithLabel("Copy of Exhibition Catalog")
                .WithClassifiedAs(Aat.ExhibitionCatalog, Aat.TypeOfWork);

            catalog.Shows = [
                new VisualItem()
                    .WithId($"{Documentation.IdRoot}/visual/spring")
                    .WithLabel("Visual Content of Spring"),
                new VisualItem()
                    .WithId($"{Documentation.IdRoot}/visual/houses")
                    .WithLabel("Visual Content of Houses in Provence")
            ];

            catalog.Carries = [
                new LinguisticObject()
                    .WithId($"{Documentation.IdRoot}/text/catalogtext")
                    .WithLabel("Exhibition Catalog Text")
            ];

            Documentation.Save(catalog);
        }


        private static void Harpers_Poster()
        {
            var harpers = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/harpers/1")
                .WithLabel("Poster Item")
                .WithClassifiedAs(Aat.Print, Aat.TypeOfWork);

            harpers.Shows = [
                new VisualItem()
                    .WithId($"{Documentation.IdRoot}/visual/harpers")
                    .WithLabel("Visual Content of Harpers"),
            ];

            Documentation.Save(harpers);
        }

        private static void Harpers_Visual_Content()
        {
            var harpersVis = new VisualItem()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/visual/harpers/1")
                .WithLabel("Visual Content of Harpers");

            harpersVis.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Aat.Description, Aat.BriefText)
                    .WithContent("The text and image are primarily red and black")
            ];

            harpersVis.Represents = [Aat.Woman];

            Documentation.Save(harpersVis);
        }

        private static void Harpers_Textual_Content()
        {
            var harpersText = new LinguisticObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/text/harpers/1")
                .WithLabel("Textual component of Harpers")
                .WithContent("Harper's. January contains Roden's corner. A Novel by Henry Seton Merriman [...]");

            harpersText.PartOf = [
                new VisualItem()
                    .WithId($"{Documentation.IdRoot}/visual/harpers")
                    .WithLabel("Visual Content of Harpers")
            ];

            Documentation.Save(harpersText);
        }

    }
}
