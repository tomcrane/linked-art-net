
namespace LinkedArtNet.Vocabulary
{
    public class RightsStatements
    {
        public static readonly string CreativeCommons = "https://creativecommons.org/";

        public static LinkedArtObject CreativeCommonsType(string label, string idPart)
        {
            return new LinkedArtObject(Types.Type).WithLabel(label).WithId($"{CreativeCommons}{idPart}");
        }

        public static LinkedArtObject CreativeCommonsPublicDomain => CreativeCommonsType("Public Domain", "publicdomain/zero/1.0/");
    }
}
