using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples
{
    public class Aboutness
    {
        // https://linked.art/model/object/aboutness/


        [LinkedArtExample("object/spring/5")]
        public static HumanMadeObject Spring_5_Description()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/spring/5")
                .WithLabel("Jeanne (Spring) by Manet");


            spring.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Aat.Description, Aat.Statement)
                    .WithContent("A chic young woman in a day dress with floral accents holds a parasol against a background of exuberant foliage.")
            ];

            spring.Shows = [
                new VisualItem()
                    .WithId("https://linked.art/example/visual/spring")
                    .WithLabel("Visual Content of Spring")
            ];

            return spring;
        }


        [LinkedArtExample("visual/spring/1")]
        public static VisualItem Spring_Visual_Content()
        {
            var springVis = new VisualItem()
                .WithContext()
                .WithId("https://linked.art/example/visual/spring/1")
                .WithLabel("Visual Content of Spring");

            springVis.Represents = [
                new Person()
                    .WithId("https://linked.art/example/person/jeanne")
                    .WithLabel("Jeanne Demarsy")
            ];

            return springVis;
        }


        [LinkedArtExample("visual/spring/5")]
        public static VisualItem Spring_Respresents_Instance_of_Type()
        {
            var springVis = new VisualItem()
                .WithContext()
                .WithId("https://linked.art/example/visual/spring/5")
                .WithLabel("Visual Content of Spring");

            springVis.RepresentsInstanceOfType = [
                Aat.Term("300046218", "Parasol")
            ];

            return springVis;
        }


        [LinkedArtExample("visual/spring/2")]
        public static VisualItem Spring_About()
        {
            var springVis = new VisualItem()
                .WithContext()
                .WithId("https://linked.art/example/visual/spring/2")
                .WithLabel("Visual Content of Spring");

            springVis.About = [
                Aat.Term("300133097", "Spring (season)")
            ];

            return springVis;
        }

        [LinkedArtExample("visual/spring/3")]
        public static VisualItem Spring_Style()
        {
            var springVis = new VisualItem()
                .WithContext()
                .WithId("https://linked.art/example/visual/spring/3")
                .WithLabel("Visual Content of Spring")
                .WithClassifiedAs(Aat.Term("300021503", "Impressionism"), Aat.Style);

            return springVis;
        }


        [LinkedArtExample("visual/spring/4")]
        public static VisualItem Spring_Portrait()
        {
            var springVis = new VisualItem()
                .WithContext()
                .WithId("https://linked.art/example/visual/spring/4")
                .WithLabel("Visual Content of Spring")
                .WithClassifiedAs(Aat.Term("300015637", "Portrait"));

            return springVis;
        }


        [LinkedArtExample("object/nightwatch/18")]
        public static HumanMadeObject Nightwatch_Signature()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/18");

            nightWatch.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Aat.SignatureStatement, Aat.Statement)
                    .WithContent("signature and date: ‘Rembrandt f 1642’")
            ];

            return nightWatch;
        }


        [LinkedArtExample("object/catalog/1")]
        public static HumanMadeObject Exhibition_Catalog_1()
        {
            var catalog = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/catalog/1")
                .WithLabel("Copy of Exhibition Catalog")
                .WithClassifiedAs(Aat.ExhibitionCatalog, Aat.TypeOfWork);

            catalog.Shows = [
                new VisualItem()
                    .WithId("https://linked.art/example/visual/spring")
                    .WithLabel("Visual Content of Spring"),
                new VisualItem()
                    .WithId("https://linked.art/example/visual/houses")
                    .WithLabel("Visual Content of Houses in Provence")
            ];

            catalog.Carries = [
                new LinguisticObject()
                    .WithId("https://linked.art/example/text/catalogtext")
                    .WithLabel("Exhibition Catalog Text")
            ];

            return catalog;
        }


        [LinkedArtExample("object/harpers/1")]
        public static HumanMadeObject Harpers_Poster()
        {
            var harpers = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/harpers/1")
                .WithLabel("Poster Item")
                .WithClassifiedAs(Aat.Print, Aat.TypeOfWork);

            harpers.Shows = [
                new VisualItem()
                    .WithId("https://linked.art/example/visual/harpers")
                    .WithLabel("Visual Content of Harpers"),
            ];

            return harpers;
        }

        [LinkedArtExample("visual/harpers/1")]
        public static VisualItem Harpers_Visual_Content()
        {
            var harpersVis = new VisualItem()
                .WithContext()
                .WithId("https://linked.art/example/visual/harpers/1")
                .WithLabel("Visual Content of Harpers");

            harpersVis.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Aat.Description, Aat.Statement)
                    .WithContent("The text and image are primarily red and black")
            ];

            harpersVis.Represents = [Aat.Term("300025943", "Woman")];

            return harpersVis;
        }

        [LinkedArtExample("text/harpers/1")]
        public static LinguisticObject Harpers_Textual_Content()
        {
            var harpersText = new LinguisticObject()
                .WithContext()
                .WithId("https://linked.art/example/text/harpers/1")
                .WithLabel("Textual component of Harpers")
                .WithContent("Harper's. January contains Roden's corner. A Novel by Henry Seton Merriman [...]");

            harpersText.PartOf = [
                new VisualItem()
                    .WithId("https://linked.art/example/visual/harpers")
                    .WithLabel("Visual Content of Harpers")
            ];

            return harpersText;
        }

    }
}
