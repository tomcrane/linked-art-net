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

    // These are utility methods; this class could get very long, but you might want to constrain to a reduced set

    public static LinkedArtObject Painting => AatType("Painting", "300033618");
    public static LinkedArtObject Sculpture => AatType("Sculpture", "300047090");
    public static LinkedArtObject Artwork => AatType("Artwork", "300133025");
    public static LinkedArtObject TypeOfWork => AatType("Type of Work", "300435443");
    public static LinkedArtObject PartType => AatType("Part Type", "300241583");



    public static LinkedArtObject Language(string aatCode, string label)
    {
        return new LinkedArtObject(Types.Language) { Id = $"{Aat}{aatCode}", Label = label };
    }

    //private static Dictionary<string, LinkedArtObject> _
}
