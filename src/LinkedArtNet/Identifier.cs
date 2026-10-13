using LinkedArtNet.Vocabulary;

namespace LinkedArtNet;

public class Identifier : LinkedArtObject
{
    public Identifier() { Type = nameof(Identifier); }

    public Identifier(string content)
    {
        Type = nameof(Identifier);
        Content = content;
    }

    public static Identifier SortValue(string value, LinkedArtObject? influencedBy = null)
    {
        var identifier = new Identifier(value).WithClassifiedAs(Getty.SortValue);
        if (influencedBy != null)
        {
            identifier.AssignedBy = [
                new Activity(Types.AttributeAssignment)
                {
                    InfluencedBy = [ influencedBy ]
                }
            ];
        }
        return identifier;
    }
}
