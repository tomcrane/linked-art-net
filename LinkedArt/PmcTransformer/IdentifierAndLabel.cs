
namespace PmcTransformer
{
    public class IdentifierAndLabel
    {
        public int Score {  get; set; }
        public required string Identifier { get; set; }
        public required string Label { get; set; }

        public override string ToString()
        {
            return $"{Identifier} ({Label}) [{Score}]";
        }
    }


    public class AuthorityStringWithSource
    {
        public required string Source { get; set; }
        public required string String { get; set; }
        public string? Authority { get; set; }
        public DateTime? Processed { get; set; }
    }
}
