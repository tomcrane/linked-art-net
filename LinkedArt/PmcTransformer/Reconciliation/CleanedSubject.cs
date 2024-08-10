
using PmcTransformer.Helpers;

namespace PmcTransformer.Reconciliation
{
    public class CleanedSubject
    {
        public required string RecordId { get; set; }
        public required string RecordKeywords { get; set; }
        public required string KeywordsCleaned { get; set; }

        public string? Aat { get; set; }
        public string? Tgn { get; set; }
        public string? Ulan { get; set; }

        public bool IsReconciled()
        {
            return Aat.HasText() || Tgn.HasText() || Ulan.HasText();
        }

        public string? SuggestType()
        {
            if(Tgn.HasText())
            {
                return "Place";
            }
            if(Aat.HasText())
            {
                return "Type"; // only used for concepts here
            }
            // we can't be sure for ULAN - Person or Group
            return null;
        }

        public Authority ToAuthority()
        {
            return new Authority
            {
                Aat = Aat,
                Tgn = Tgn,
                Ulan = Ulan,
                Label = KeywordsCleaned,
                Type = SuggestType()
            };                
        }
    }
}
