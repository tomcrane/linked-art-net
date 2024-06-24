using System.Diagnostics.SymbolStore;

namespace LinkedArtNet.Vocabulary;

public class Getty
{
    public static readonly string Aat = "http://vocab.getty.edu/aat/";

    public static LinkedArtObject AatType(string? label, string idPart)
    {
        return new LinkedArtObject(Types.Type).WithLabel(label).WithId($"{Aat}{idPart}");
    }

    // Names
    public static LinkedArtObject PrimaryName => AatType("Primary Name", "300404670");
    public static LinkedArtObject GivenName => AatType("Given Name", "300404651");
    public static LinkedArtObject MiddleName => AatType("Middle Name", "300404654");
    public static LinkedArtObject FamilyName => AatType("Family Name", "300404652");

    // WorkTypes
    public static LinkedArtObject TypeOfWork => AatType("Type of Work", "300435443");
    public static LinkedArtObject Painting => AatType("Painting", "300033618");
    public static LinkedArtObject Photograph => AatType("Photograph", "300046300");
    public static LinkedArtObject Print => AatType("Print", "300041273");
    public static LinkedArtObject Sculpture => AatType("Sculpture", "300047090");
    public static LinkedArtObject Artwork => AatType("Artwork", "300133025");
    public static LinkedArtObject Book => AatType("Book", "300028051");
    public static LinkedArtObject Monograph => AatType("Monograph", "300060417");
    public static LinkedArtObject Letter => AatType("Letter", "300026879");

    //Archives
    public static LinkedArtObject Archives => AatType("archives (groupings)", "300375748");
    public static LinkedArtObject RecordIdentifiers => AatType("Record Identifiers", "300435704");
    public static LinkedArtObject ArchivalGrouping => AatType("Archival Grouping", "300404022");
    public static LinkedArtObject ArchivalSubGrouping => AatType("Archival SubGrouping", "300404023");
    public static LinkedArtObject ArchivalMaterials => AatType("Archival Materials", "300379505");

    // Parts
    public static LinkedArtObject PartType => AatType("Part Type", "300241583");
    public static LinkedArtObject BackPart => AatType("Back Part", "300190692");
    public static LinkedArtObject Support => AatType("Support", "300014844");

    // Matter
    public static LinkedArtObject DimensionStatement => AatType("Dimension Statement", "300435430");
    public static LinkedArtObject MaterialStatement => AatType("Material Statement", "300435429");
    public static LinkedArtObject Shape => AatType("Shape", "300056273");

    // Texts
    public static LinkedArtObject BriefText => AatType("Brief Text", "300418049");
    public static LinkedArtObject DisplayTitle => AatType("Display Title", "300404669");
    public static LinkedArtObject Description => AatType("Description", "300435416");
    public static LinkedArtObject Inscription => AatType("Inscription", "300435414");
    public static LinkedArtObject BiographyStatement => AatType("Biography Statement", "300435422");
    public static LinkedArtObject CreditStatement => AatType("Credit Statement", "300026687");
    public static LinkedArtObject CopyrightLicenseStatement => AatType("Copyright/License Statement", "300435434");
    public static LinkedArtObject PhysicalStatement => AatType("Physical Statement", "300435452");
    public static LinkedArtObject PaginationStatement => AatType("Pagination Statement", "300435440");
    public static LinkedArtObject BibliographyStatement => AatType("Bibliography Statement", "300026497");
    public static LinkedArtObject EditionStatement => AatType("Edition Statement", "300435435");

    // Identifiers
    public static LinkedArtObject SystemAssignedNumber => AatType("System-Assigned Number", "300435704");
    public static LinkedArtObject AccessionNumber => AatType("Accession Number", "300312355");
    public static LinkedArtObject SortValue => AatType("Sort Value", "300456575");
    public static LinkedArtObject ISBN => AatType("ISBN", "300417443");




    public static LinkedArtObject ProvenanceActivity => AatType("Provenance Activity", "300055863");

    public static LinkedArtObject WebPage => AatType("Web Page", "300264578");
    public static LinkedArtObject DigitalImage => AatType("Digital Image", "300215302");
    public static LinkedArtObject EmailAddress => AatType("Email Address", "300435686");


    public static LinkedArtObject Style => AatType("Style", "300015646");


    public static LinkedArtObject Building => AatType("Building", "300004792");
    public static LinkedArtObject TelephoneNumber => AatType("Telephone Number", "300435688");
    public static LinkedArtObject StreetAddress => AatType("Street Address", "300386983");
    public static LinkedArtObject City => AatType("City", "300008389");
    public static LinkedArtObject Nation => AatType("Nation", "300128207");



    public static LinkedArtObject Nationality => AatType("Nationality", "300379842");
    public static LinkedArtObject Ethnicity => AatType("Ethnicity", "300250435");
    public static LinkedArtObject Gender => AatType("Gender", "300055147");


    public static LinkedArtObject Exhibiting => AatType("Exhibiting", "300054766");
    public static LinkedArtObject Exhibition => AatType("Exhibition", "300417531");
    public static LinkedArtObject Museum => AatType("Museum", "300312281");
    public static LinkedArtObject Collection => AatType("Collection", "300025976");


    public static LinkedArtObject Publishing => AatType("Publishing", "300054686");


    public static LinkedArtObject Language(string aatCode, string label)
    {
        return new LinkedArtObject(Types.Language) { Id = $"{Aat}{aatCode}", Label = label };
    }

    //private static Dictionary<string, LinkedArtObject> _
}
