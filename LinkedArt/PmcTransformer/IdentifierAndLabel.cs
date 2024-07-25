
namespace PmcTransformer
{
    public class IdentifierAndLabel
    {
        public required string Identifier { get; set; }
        public required string Label { get; set; }
    }


    public class SourceStringAndAuthority
    {
        public required string Source { get; set; }
        public required string String { get; set; }
        public string? Authority { get; set; }
        public DateTime? Processed { get; set; }
    }
}
