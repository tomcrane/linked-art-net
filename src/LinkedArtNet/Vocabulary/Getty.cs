namespace LinkedArtNet.Vocabulary;

/// <summary>
/// Getty vocabulary URI bases. Terms live in <see cref="Aat"/> (registry constants plus <c>Aat.Term</c> and friends);
/// places and actors are built with <see cref="Tgn.Place"/>, <see cref="Ulan.Person"/> and <see cref="Ulan.Group"/>.
/// </summary>
public static class Getty
{
    public static readonly string Aat = Vocabulary.Aat.Base;
    public static readonly string Tgn = Vocabulary.Tgn.Base;
    public static readonly string Ulan = Vocabulary.Ulan.Base;

    public const string PrimaryNameId = "http://vocab.getty.edu/aat/300404670";
}
