namespace LinkedArtNet.Vocabulary;

/// <summary>
/// Getty vocabulary URI bases and ad-hoc reference factories. Named terms live in the generated registries
/// <see cref="Aat"/>, <see cref="Tgn"/> and <see cref="Ulan"/> (from <c>vocabulary/*.json</c>).
/// </summary>
public static class Getty
{
    public static readonly string Aat = Vocabulary.Aat.Base;
    public static readonly string Tgn = Vocabulary.Tgn.Base;
    public static readonly string Ulan = Vocabulary.Ulan.Base;

    public const string PrimaryNameId = "http://vocab.getty.edu/aat/300404670";

    /// <summary>A <see cref="Type"/> for an AAT id with an explicit label. Prefer a named <see cref="Aat"/> term when one exists.</summary>
    public static Type AatType(string? label, string idPart) => Vocabulary.Aat.Of(idPart, label);

    public static Place TgnPlace(string? label, string idPart) => Vocabulary.Tgn.Of(idPart, label);
    public static Person UlanPerson(string? label, string idPart) => Vocabulary.Ulan.PersonOf(idPart, label);
    public static Group UlanGroup(string? label, string idPart) => Vocabulary.Ulan.GroupOf(idPart, label);

    /// <summary>A <see cref="Language"/> for an AAT id with an explicit label and notation (the site's examples are not always consistent).</summary>
    public static Language Language(string aatCode, string label, string? notation) => Vocabulary.Aat.OfLanguage(aatCode, label, notation);

    public static Type Nationality(string aatCode, string label) => Vocabulary.Aat.NationalityOf(aatCode, label);
}
