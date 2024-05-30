using LinkedArtNet.Vocabulary;

namespace LinkedArtNet
{
    public class Identifier : LinkedArtObject
    {
        public Identifier() { Type = nameof(Identifier); }

        public Identifier(string content)
        {
            Type = nameof(Identifier);
            Content = content;
        }

        public static Identifier SortValue(string value, LinkedArtObject? motivatedBy = null)
        {
            var identifier = new Identifier(value).WithClassifiedAs(Getty.SortValue);
            if (motivatedBy != null)
            {
                identifier.AssignedBy = [
                    new Activity(Types.AttributeAssignment)
                    {
                        MotivatedBy = [motivatedBy]
                    }
                ];
            }
            return identifier;
        }
    }
}
