using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples;

// https://linked.art/model/digital/

public class Digital
{

    [LinkedArtExample("digital/operation_nw/1")]
    public static DigitalObject Nightwatch_Digital_Publication()
    {
        var nightwatchPublication = new DigitalObject()
            .WithContext()
            .WithId("https://linked.art/example/digital/operation_nw/1")
            .WithLabel("Operation Night Watch Publication")
            .WithClassifiedAs(Aat.WebPage);

        nightwatchPublication.IdentifiedBy = [new Name("Operation Night Watch")];
        nightwatchPublication.WithFileSize(220, MeasurementUnit.Kilobytes, "220 kb");
        nightwatchPublication.AccessPoint = [
            new DigitalObject().WithId("https://www.rijksmuseum.nl/en/stories/operation-night-watch")
        ];
        nightwatchPublication.Format = "text/html";
        nightwatchPublication.ConformsTo = [
            new InformationObject().WithId("http://w3.org/TR/html")
        ];

        var creation = new Creation()
        {
            TimeSpan = new LinkedArtTimeSpan
            {
                BeginOfTheBegin = new LinkedArtDate(2019, 7, 19),
                EndOfTheEnd = new LinkedArtDate(2019, 7, 21)
            },
            CarriedOutBy = [
                new Group()
                    .WithId("https://linked.art/example/group/rijksmuseum")
                    .WithLabel("Rijksmuseum")
        ]
        };
        nightwatchPublication.CreatedBy = creation;

        nightwatchPublication.DigitallyCarries = [
            new LinguisticObject()
                .WithId("https://linked.art/example/text/operation_nw_en")
                .WithLabel("Operation Night Watch in English")
        ];


        return nightwatchPublication;
    }


    [LinkedArtExample("object/spring/8")]
    public static HumanMadeObject Spring_Physical_Shows()
    {
        var spring = new HumanMadeObject()
            .WithContext()
            .WithId("https://linked.art/example/object/spring/8")
            .WithLabel("Jeanne (Spring) by Manet");

        spring.IdentifiedBy = [new Name("Jeanne (Spring)")];
        spring.Shows = [
            new VisualItem()
                .WithId("https://linked.art/example/visual/spring")
                .WithLabel("Visual Content of Spring")
        ];

        return spring;
    }


    [LinkedArtExample("digital/spring/1")]
    public static DigitalObject Spring_Digitally_Shows()
    {
        var spring = new DigitalObject()
            .WithContext()
            .WithId("https://linked.art/example/digital/spring/1")
            .WithLabel("Jeanne (Spring) (Digital)");

        spring.IdentifiedBy = [new Name("Jeanne (Spring)")];

        spring.DigitallyShows = [
            new VisualItem()
                .WithId("https://linked.art/example/visual/spring")
                .WithLabel("Visual Content of Spring")
        ];

        return spring;
    }


    [LinkedArtExample("person/rembrandt/15")]
    public static Person Rembrandt_Representation()
    {
        var rembrandt = new Person()
            .WithContext()
            .WithId("https://linked.art/example/person/rembrandt/15")
            .WithLabel("Rembrandt");

        var visual = new VisualItem();
        var digitalImage = new DigitalObject()
            .WithLabel("Image of Rembrandt")
            .WithClassifiedAs(Aat.DigitalImage);
        digitalImage.Format = "image/jpeg";
        digitalImage.AccessPoint = [
            new DigitalObject().WithId("https://upload.wikimedia.org/wikipedia/commons/b/bd/Rembrandt_van_Rijn_-_Self-Portrait_-_Google_Art_Project.jpg")
        ];
        visual.DigitallyShownBy = [digitalImage];
        rembrandt.Representation = [visual];

        return rembrandt;
    }


    [LinkedArtExample("object/spring/9")]
    public static HumanMadeObject Spring_Web_Page()
    {
        var spring = new HumanMadeObject()
            .WithContext()
            .WithId("https://linked.art/example/object/spring/9")
            .WithLabel("Jeanne (Spring) by Manet")
            .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

        spring.IdentifiedBy = [new Name("Jeanne (Spring)")];

        var springWebLinguistic = new LinguisticObject();
        var springWebDigital = new DigitalObject()
            .WithClassifiedAs(Aat.WebPage);
        springWebDigital.Format = "text/html";
        springWebDigital.AccessPoint = [new DigitalObject().WithId("https://www.getty.edu/art/collection/object/103QTZ")];
        springWebLinguistic.DigitallyCarriedBy = [springWebDigital];
        spring.SubjectOf = [springWebLinguistic];

        return spring;
    }


    [LinkedArtExample("object/spring/10")]
    public static HumanMadeObject Spring_IIIF_Manifest()
    {
        var spring = new HumanMadeObject()
            .WithContext()
            .WithId("https://linked.art/example/object/spring/10")
            .WithLabel("Jeanne (Spring) by Manet")
            .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

        spring.IdentifiedBy = [new Name("Jeanne (Spring)")];
        spring.SubjectOf = [
            new IIIFManifest(
                Presentation.Version3, 
                "https://media.getty.edu/iiif/manifest/db379bba-801c-4650-bc31-3ff2f712eb21")
        ];

        return spring;
    }


    [LinkedArtExample("object/spring/11")]
    public static HumanMadeObject Spring_IIIF_Image()
    {
        var spring = new HumanMadeObject()
            .WithContext()
            .WithId("https://linked.art/example/object/spring/11")
            .WithLabel("Jeanne (Spring) by Manet")
            .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

        spring.IdentifiedBy = [new Name("Jeanne (Spring)")];

        var visual = new VisualItem();
        var digitalImage = new DigitalObject()
            .WithClassifiedAs(Aat.DigitalImage);
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

        return spring;
    }
}
