namespace LinkedArtNet.Vocabulary;

public static class RightsStatements
{
    public static readonly string CreativeCommons = "https://creativecommons.org/";

    public static Type CreativeCommonsType(string label, string idPart) => new($"{CreativeCommons}{idPart}", label);

    public static Type CreativeCommonsPublicDomain => CreativeCommonsType("Public Domain", "publicdomain/zero/1.0/");
}
