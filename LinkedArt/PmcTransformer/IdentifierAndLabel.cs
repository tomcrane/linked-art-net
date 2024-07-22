
namespace PmcTransformer
{
    public class IdentifierAndLabel
    {
        public required string Identifier { get; set; }
        public required string Label { get; set; }
    }


    public class SourceStringAndAuthority
    {
        public required string SourceString { get; set; }
        public string? Authority { get; set; }
    }
}
