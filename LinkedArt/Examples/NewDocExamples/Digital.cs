using LinkedArtNet.Vocabulary;
using LinkedArtNet;

namespace Examples.NewDocExamples
{
    // https://linked.art/model/digital/

    public class Digital
    {
        public static void Create()
        {
            Nightwatch_Digital_Publication();
            Spring_Physical_Shows();
            Spring_Digitally_Shows();
            Rembrandt_Representation();
            Spring_Web_Page();
            Spring_IIIF_Manifest();
            Spring_IIIF_Image();
        }


        private static void Nightwatch_Digital_Publication()
        {
            var nightwatchPublication = new DigitalObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/digital/operation_nw/1")
                .WithLabel("Operation Night Watch Publication")
                .WithClassifiedAs(Getty.WebPage);

            nightwatchPublication.IdentifiedBy = [new Name("Operation Night Watch")];
            nightwatchPublication.WithFileSize(220, MeasurementUnit.Kilobytes, "220 kb");
            nightwatchPublication.AccessPoint = [
                new DigitalObject().WithId("https://www.rijksmuseum.nl/en/stories/operation-night-watch")
            ];
            nightwatchPublication.Format = "text/html";
            nightwatchPublication.ConformsTo = [
                new LinkedArtObject(Types.InformationObject).WithId("http://w3.org/TR/html")
            ];

            var creation = new Activity(Types.Creation)
            {
                TimeSpan = new LinkedArtTimeSpan
                {
                    BeginOfTheBegin = new LinkedArtDate(2019, 7, 19),
                    EndOfTheEnd = new LinkedArtDate(2019, 7, 21)
                },
                CarriedOutBy = [
                    new Group()
                        .WithId($"{Documentation.IdRoot}/group/rijksmuseum")
                        .WithLabel("Rijksmuseum")
            ]
            };
            nightwatchPublication.CreatedBy = creation;

            nightwatchPublication.DigitallyCarries = [
                new LinkedArtObject(Types.LinguisticObject)
                    .WithId($"{Documentation.IdRoot}/text/operation_nw_en")
                    .WithLabel("Operation Night Watch in English")
            ];


            Documentation.Save(nightwatchPublication);
        }


        private static void Spring_Physical_Shows()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/spring/8")
                .WithLabel("Jeanne (Spring) by Manet");

            spring.IdentifiedBy = [new Name("Jeanne (Spring)")];
            spring.Shows = [
                new Work(Types.VisualItem)
                    .WithId($"{Documentation.IdRoot}/visual/spring")
                    .WithLabel("Visual Content of Spring")
            ];

            Documentation.Save(spring);
        }


        private static void Spring_Digitally_Shows()
        {
            var spring = new DigitalObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/digital/spring/1")
                .WithLabel("Jeanne (Spring) (Digital)");

            spring.IdentifiedBy = [new Name("Jeanne (Spring)")];

            spring.DigitallyShows = [
                new Work(Types.VisualItem)
                    .WithId($"{Documentation.IdRoot}/visual/spring")
                    .WithLabel("Visual Content of Spring")
            ];

            Documentation.Save(spring);
        }


        private static void Rembrandt_Representation()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/person/rembrandt/15")
                .WithLabel("Rembrandt");

            var visual = new Work(Types.VisualItem);
            var digitalImage = new DigitalObject()
                .WithLabel("Image of Rembrandt")
                .WithClassifiedAs(Getty.DigitalImage);
            digitalImage.Format = "image/jpeg";
            digitalImage.AccessPoint = [
                new DigitalObject().WithId("https://upload.wikimedia.org/wikipedia/commons/b/bd/Rembrandt_van_Rijn_-_Self-Portrait_-_Google_Art_Project.jpg")
            ];
            visual.DigitallyShownBy = [digitalImage];
            rembrandt.Representation = [visual];

            Documentation.Save(rembrandt);
        }


        private static void Spring_Web_Page()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/spring/9")
                .WithLabel("Jeanne (Spring) by Manet")
                .WithClassifiedAs(Getty.Painting, Getty.TypeOfWork);

            spring.IdentifiedBy = [new Name("Jeanne (Spring)")];

            var springWebLinguistic = new LinkedArtObject(Types.LinguisticObject);
            var springWebDigital = new DigitalObject()
                .WithClassifiedAs(Getty.WebPage);
            springWebDigital.Format = "text/html";
            springWebDigital.AccessPoint = [new DigitalObject().WithId("https://www.getty.edu/art/collection/object/103QTZ")];
            springWebLinguistic.DigitallyCarriedBy = [springWebDigital];
            spring.SubjectOf = [springWebLinguistic];

            Documentation.Save(spring);
        }


        private static void Spring_IIIF_Manifest()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/spring/10")
                .WithLabel("Jeanne (Spring) by Manet")
                .WithClassifiedAs(Getty.Painting, Getty.TypeOfWork);

            spring.IdentifiedBy = [new Name("Jeanne (Spring)")];
            spring.SubjectOf = [
                new IIIFManifest(
                    Presentation.Version3, 
                    "https://media.getty.edu/iiif/manifest/db379bba-801c-4650-bc31-3ff2f712eb21")
            ];

            Documentation.Save(spring);
        }


        private static void Spring_IIIF_Image()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/spring/11")
                .WithLabel("Jeanne (Spring) by Manet")
                .WithClassifiedAs(Getty.Painting, Getty.TypeOfWork);

            spring.IdentifiedBy = [new Name("Jeanne (Spring)")];

            var visual = new Work(Types.VisualItem);
            var digitalImage = new DigitalObject()
                .WithClassifiedAs(Getty.DigitalImage);
            digitalImage.AccessPoint = [
                new DigitalObject().WithId("https://media.getty.edu/iiif/image/8094f61e-e458-42bd-90cf-a0ed0dcc90b9/full/full/0/default.jpg")
            ];

            digitalImage.DigitallyAvailableVia = [
                new IIIFImageService(
                    Image.Version3,
                    "https://media.getty.edu/iiif/image/8094f61e-e458-42bd-90cf-a0ed0dcc90b9")
                ];

            visual.DigitallyShownBy = [digitalImage];
            spring.Representation = [visual];

            Documentation.Save(spring);
        }
    }
}
