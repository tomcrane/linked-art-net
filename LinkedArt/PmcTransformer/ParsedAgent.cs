using LinkedArtNet.Parsers;
using PmcTransformer.Helpers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace PmcTransformer
{
    public partial class ParsedAgent
    {
        public required string Original { get; set; }

        public required string NormalisedOriginal { get; set; }

        public string? Name { get; set; }

        public string? DateString { get; set; }

        public string? NumericDateString { get; set; }

        public string? Role { get; set; }

        public string? Honorific { get; set; }

        // e.g., Crane, Thomas
        // Not yet dealing with surname, firstnames reordering - honorifics make this hard
        public string? NormalisedName { get; private set; }

        // e.g., Crane, Thomas [1971-]
        public string? NormalisedNameWithDates { get; private set; }

        // e.g., Crane, Thomas [1971-] (developer)
        public string? NormalisedFullForm { get; private set; }


        // e.g., Crane, Thomas, 1971-
        public string? NormalisedLocForm { get; private set; }

        public bool IsActive { get; set; }
        public bool IsApproximate { get; set; }

        public int? StartYear { get; set; }

        public int? EndYear { get; set; }

        // identifiers associated with the original string
        public List<string> Identifiers { get; set; } = [];



        [GeneratedRegex(@"^(.*)\((.*)\)$")]
        private static partial Regex RolePattern();

        [GeneratedRegex(@"^(.*)\[(.*)\]$")]
        private static partial Regex DatePattern();


        [SetsRequiredMembers]
        public ParsedAgent(string original)
        {
            Original = original;
            NormalisedOriginal = original.Trim().TrimEnd('.').Trim().TrimEnd(',').Trim().TrimOuterBrackets()!;

            string working = NormalisedOriginal!;

            string personDatePart;

            var roleMatch = RolePattern().Match(working);
            if (roleMatch.Success)
            {
                personDatePart = roleMatch.Groups[1].Value.Trim();
                Role = roleMatch.Groups[2].Value.Trim();
            }
            else
            {
                personDatePart = working;
            }

            var dateMatch = DatePattern().Match(personDatePart);
            if (dateMatch.Success)
            {
                Name = dateMatch.Groups[1].Value.Trim();
                if(Name.HasText())
                {
                    DateString = dateMatch.Groups[2].Value.Trim();

                    var commaParts = DateString.Split(',');
                    if (commaParts.Length > 1 && commaParts[1].Trim().HasText())
                    {
                        Honorific = commaParts[0].Trim();
                        DateString = string.Join(",", commaParts[1..]).Trim();
                    }

                    var tsHints = TimespanParser.ParseLibraryAgentDate(DateString);
                    IsActive = tsHints.IsDatesActive;
                    IsApproximate = tsHints.IsCirca;
                    NumericDateString = tsHints.NumericDateString;
                    StartYear = tsHints.StartYear;
                    EndYear = tsHints.EndYear;
                }
                else
                {
                    Name = personDatePart.Trim().TrimOuterBrackets();
                }
            }
            else
            {
                // No name
                Name = personDatePart.Trim().TrimOuterBrackets();
            }
                            

            var normSB = new StringBuilder();
            var normLoc = new StringBuilder();
            normSB.Append(Name);
            normLoc.Append(Name);
            NormalisedName = Name;
            // TODO - work in Honorifics
            if (DateString.HasText())
            {
                normSB.Append(" [");
                normLoc.Append(", ");
                if (IsActive)
                {
                    normSB.Append("active ");
                }
                if (IsApproximate)
                {
                    normSB.Append("approximately ");
                }
                if(NumericDateString.HasText())
                {
                    normSB.Append(NumericDateString);
                    normLoc.Append(NumericDateString);
                }
                else
                {
                    normSB.Append(DateString);
                    normLoc.Append(DateString);
                }
                normSB.Append(']');
            }
            
            NormalisedNameWithDates = normSB.ToString();
            NormalisedLocForm = normLoc.ToString();

            if(Role.HasText())
            {
                normSB.Append($" ({Role.TrimOuterParentheses()})");
            }
            NormalisedFullForm = normSB.ToString();
        }

        public override string ToString()
        {
            return $"{Original} => {NormalisedFullForm}";
        }
    }
}
