namespace LinkedArtNet.Vocabulary
{
    public class IIIFManifest : LinkedArtObject
    {
        public IIIFManifest(string format, string id)
        {
            Type = nameof(Types.LinguisticObject);
            var IIIFDigital = new DigitalObject
            {
                Format = format,
                AccessPoint = [
                    new DigitalObject().WithId(id)
                ],
                ConformsTo = [
                    new LinkedArtObject(Types.InformationObject)
                        .WithId("http://iiif.io/api/presentation/")
                ]
            };
            DigitallyCarriedBy = [IIIFDigital];
        }
    }

    public class IIIFImageService : DigitalService
    {
        public IIIFImageService(string format, string id) 
        {
            AccessPoint = [
                new DigitalObject().WithId(id)
            ];
            ConformsTo = [
                new LinkedArtObject(Types.InformationObject)
                    .WithId("http://iiif.io/api/image")
            ];
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
}
