namespace LinkedArtNet.Vocabulary;

/// <summary>A IIIF Presentation API Manifest, modelled as a text carried by a digital object.</summary>
public class IIIFManifest : LinguisticObject
{
    public IIIFManifest(string format, string id)
    {
        DigitallyCarriedBy =
        [
            new DigitalObject
            {
                Format = format,
                AccessPoint = [new DigitalObject().WithId(id)],
                ConformsTo = [new InformationObject("http://iiif.io/api/presentation/")],
            },
        ];
    }
}

/// <summary>A IIIF Image API service.</summary>
public class IIIFImageService : DigitalService
{
    public IIIFImageService(string format, string id)
    {
        AccessPoint = [new DigitalObject().WithId(id)];
        ConformsTo = [new InformationObject("http://iiif.io/api/image")];
        Format = format;
    }
}

public static class Presentation
{
    public const string Version2 = "application/ld+json;profile='http://iiif.io/api/presentation/2/context.json'";
    public const string Version3 = "application/ld+json;profile='http://iiif.io/api/presentation/3/context.json'";
}

public static class Image
{
    public const string Version2 = "application/ld+json;profile='http://iiif.io/api/image/2/context.json'";
    public const string Version3 = "application/ld+json;profile='http://iiif.io/api/image/3/context.json'";
}
