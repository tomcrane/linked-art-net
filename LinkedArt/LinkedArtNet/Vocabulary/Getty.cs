using System.Diagnostics.SymbolStore;

namespace LinkedArtNet.Vocabulary;

public class Getty
{
    public static readonly string Aat = "http://vocab.getty.edu/aat/";

    public static LinkedArtObject AatType(string label, string idPart)
    {
        return new LinkedArtObject(Types.Type).WithLabel(label).WithId($"{Aat}{idPart}");
    }

    public static LinkedArtObject PrimaryName => AatType("Primary Name", "300404670");
    public static LinkedArtObject GivenName => AatType("Given Name", "300404651");
    public static LinkedArtObject MiddleName => AatType("Middle Name", "300404654");
    public static LinkedArtObject FamilyName => AatType("Family Name", "300404652");

    // These are utility methods; this class could get very long, but you might want to constrain to a reduced set

    public static LinkedArtObject TypeOfWork => AatType("Type of Work", "300435443");
    public static LinkedArtObject Painting => AatType("Painting", "300033618");
    public static LinkedArtObject Photograph => AatType("Photograph", "300046300");
    public static LinkedArtObject Print => AatType("Print", "300041273");
    public static LinkedArtObject Sculpture => AatType("Sculpture", "300047090");
    public static LinkedArtObject Artwork => AatType("Artwork", "300133025");

    public static LinkedArtObject PartType => AatType("Part Type", "300241583");
    public static LinkedArtObject BackPart => AatType("Back Part", "300190692");
    public static LinkedArtObject Support => AatType("Support", "300014844");


    public static LinkedArtObject DimensionStatement => AatType("Dimension Statement", "300435430");
    public static LinkedArtObject MaterialStatement => AatType("Material Statement", "300435429");
    public static LinkedArtObject Shape => AatType("Shape", "300056273");


    public static LinkedArtObject BriefText => AatType("Brief Text", "300418049");
    public static LinkedArtObject DisplayTitle => AatType("Display Title", "300404669");
    public static LinkedArtObject Description => AatType("Description", "300435416");
    public static LinkedArtObject Inscription => AatType("Inscription", "300435414");
    public static LinkedArtObject CreditStatement => AatType("Credit Statement", "300026687");
    public static LinkedArtObject CopyrightLicenseStatement => AatType("Copyright/License Statement", "300435434");

    public static LinkedArtObject WebPage => AatType("Web Page", "300264578");
    public static LinkedArtObject DigitalImage => AatType("Digital Image", "300215302");
    public static LinkedArtObject EmailAddress => AatType("Email Address", "300435686");


    public static LinkedArtObject Style => AatType("Style", "300015646");


    public static LinkedArtObject Building => AatType("Building", "300004792");
    public static LinkedArtObject TelephoneNumber => AatType("Telephone Number", "300435688");
    public static LinkedArtObject StreetAddress => AatType("Street Address", "300386983");
    public static LinkedArtObject City => AatType("City", "300008389");
    public static LinkedArtObject Nation => AatType("Nation", "300128207");

    public static LinkedArtObject Language(string aatCode, string label)
    {
        return new LinkedArtObject(Types.Language) { Id = $"{Aat}{aatCode}", Label = label };
    }

    //private static Dictionary<string, LinkedArtObject> _
}
