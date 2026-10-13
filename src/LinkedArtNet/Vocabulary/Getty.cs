namespace LinkedArtNet.Vocabulary;

/// <summary>
/// Getty AAT terms used across Linked Art. Each property returns a new <see cref="Type"/> so callers cannot
/// mutate a shared instance. (Plan: Phase 1 step 5 replaces this hand-written list with a generated registry.)
/// </summary>
public static class Getty
{
    public static readonly string Aat = "http://vocab.getty.edu/aat/";
    public static readonly string Tgn = "http://vocab.getty.edu/tgn/";
    public static readonly string Ulan = "http://vocab.getty.edu/ulan/";

    public const string PrimaryNameId = "http://vocab.getty.edu/aat/300404670";

    public static Type AatType(string? label, string idPart) => new($"{Aat}{idPart}", label);
    public static Material AatMaterial(string? label, string idPart) => new($"{Aat}{idPart}", label);
    public static Currency AatCurrency(string? label, string idPart) => new($"{Aat}{idPart}", label);
    public static MeasurementUnit AatUnit(string? label, string idPart) => new($"{Aat}{idPart}", label);
    public static Place TgnPlace(string? label, string idPart) => new Place().WithId($"{Tgn}{idPart}").WithLabel(label);
    public static Person UlanPerson(string? label, string idPart) => new Person().WithId($"{Ulan}{idPart}").WithLabel(label);
    public static Group UlanGroup(string? label, string idPart) => new Group().WithId($"{Ulan}{idPart}").WithLabel(label);

    // Names
    public static Type PrimaryName => AatType("Primary Name", "300404670");
    public static Type GivenName => AatType("Given Name", "300404651");
    public static Type MiddleName => AatType("Middle Name", "300404654");
    public static Type FamilyName => AatType("Family Name", "300404652");

    public static Type Family => AatType("Family", "300055474");
    public static Type Organization => AatType("Organization", "300025948");

    // Work types
    public static Type TypeOfWork => AatType("Type of Work", "300435443");
    public static Type Painting => AatType("Painting", "300033618");
    public static Type Photograph => AatType("Photograph", "300046300");
    public static Type Print => AatType("Print", "300041273");
    public static Type Sculpture => AatType("Sculpture", "300047090");
    public static Type Artwork => AatType("Artwork", "300133025");
    public static Type Book => AatType("Book", "300028051");
    public static Type Monograph => AatType("Monograph", "300060417");
    public static Type Letter => AatType("Letter", "300026879");
    public static Type Sound => AatType("Sound", "300056060");

    // Archives
    public static Type Archives => AatType("archives (groupings)", "300375748");
    public static Type RecordIdentifiers => AatType("Record Identifiers", "300435704");
    public static Type ArchivalGrouping => AatType("Archival Grouping", "300404022");
    public static Type ArchivalSubGrouping => AatType("Archival SubGrouping", "300404023");
    public static Type ArchivalMaterials => AatType("Archival Materials", "300379505");
    public static Type Accruals => AatType("Accruals", "300055458");
    public static Type AccessStatement => AatType("Access Statement", "300133046");
    public static Type AdministrativeHistory => AatType("Administrative History", "300077565");
    public static Type RelatedMaterial => AatType("Related Material", "300444119");
    public static Type ArrangementDescription => AatType("Arrangement Description", "300444118");

    // Parts
    public static Type PartType => AatType("Part Type", "300241583");
    public static Type BackPart => AatType("Back Part", "300190692");
    public static Type Support => AatType("Support", "300014844");

    // Physical
    public static Type DimensionStatement => AatType("Dimension Statement", "300435430");
    public static Type MaterialStatement => AatType("Material Statement", "300435429");
    public static Type Shape => AatType("Shape", "300056273");
    public static Type Height => AatType("Height", "300055644");
    public static Type Width => AatType("Width", "300055647");
    public static Type Color => AatType("Color", "300080438");
    public static Type Count => AatType("Count", "300404433");
    public static Type FileSize => AatType("File Size", "300265863");

    // Texts
    public static Type BriefText => AatType("Brief Text", "300418049");
    public static Type DisplayTitle => AatType("Display Title", "300404669");
    public static Type Description => AatType("Description", "300435416");
    public static Type Inscription => AatType("Inscription", "300435414");
    public static Type BiographyStatement => AatType("Biography Statement", "300435422");
    public static Type CreditStatement => AatType("Credit Statement", "300026687");
    public static Type CopyrightLicenseStatement => AatType("Copyright/License Statement", "300435434");
    public static Type PhysicalStatement => AatType("Physical Statement", "300435452");
    public static Type PaginationStatement => AatType("Pagination Statement", "300435440");
    public static Type BibliographyStatement => AatType("Bibliography Statement", "300026497");
    public static Type EditionStatement => AatType("Edition Statement", "300435435");
    public static Type PublicationStatement => AatType("Publication Statement", "300202362");
    public static Type GeneralNote => AatType("General Note", "300027200");

    // Identifiers
    public static Type SystemAssignedNumber => AatType("System-Assigned Number", "300435704");
    public static Type AccessionNumber => AatType("Accession Number", "300312355");
    public static Type SortValue => AatType("Sort Value", "300456575");
    public static Type ISBN => AatType("ISBN", "300417443");
    public static Type CreationNumber => AatType("Creation Number", "300404622");

    // Provenance, digital, misc
    public static Type ProvenanceActivity => AatType("Provenance Activity", "300055863");
    public static Type ProvenanceStatement => AatType("Provenance Statement", "300435438");
    public static Type WebPage => AatType("Web Page", "300264578");
    public static Type DigitalImage => AatType("Digital Image", "300215302");
    public static Type EmailAddress => AatType("Email Address", "300435686");
    public static Type Style => AatType("Style", "300015646");
    public static Type Building => AatType("Building", "300004792");
    public static Type TelephoneNumber => AatType("Telephone Number", "300435688");
    public static Type StreetAddress => AatType("Street Address", "300386983");
    public static Type City => AatType("City", "300008389");
    public static Type Nation => AatType("Nation", "300128207");
    public static Type Ethnicity => AatType("Ethnicity", "300250435");
    public static Type Gender => AatType("Gender", "300055147");
    public static Type Exhibiting => AatType("Exhibiting", "300054766");
    public static Type Exhibition => AatType("Exhibition", "300417531");
    public static Type Museum => AatType("Museum", "300312281");
    public static Type Collection => AatType("Collection", "300025976");
    public static Type Curating => AatType("Curating", "300054277");
    public static Type Publishing => AatType("Publishing", "300054686");

    public static Language Language(string aatCode, string label, string? notation) => new($"{Aat}{aatCode}", label, notation);

    public static Type Nationality(string aatCode, string label) =>
        AatType(label, aatCode).WithClassifiedAs(AatType("Nationality", "300379842"));
}
