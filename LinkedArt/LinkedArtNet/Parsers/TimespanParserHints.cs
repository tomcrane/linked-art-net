
namespace LinkedArtNet.Parsers
{
    public class TimespanParserHints
    {
        public bool IsDatesActive { get; set; } = false;
        public bool IsCirca { get; set; } = false;


        public string? DateString { get; set; }
        public string? NumericDateString { get; set; }

        public int? StartYear { get; set; }
        public int? EndYear { get; set; }

    }
}
