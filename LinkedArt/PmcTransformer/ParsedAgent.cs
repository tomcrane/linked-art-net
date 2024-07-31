using LinkedArtNet.Parsers;
using PmcTransformer.Helpers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace PmcTransformer
{
    public partial class ParsedAgent
    {
        public required string Original {  get; set; }

        public string? Name { get; set; }

        public string? DateString { get; set; }

        public string? NumericDateString { get; set; }

        public string? Role { get; set; }

        public string? Honorific { get; set; }

        public string? NormalisedName { get; private set; }

        public string? NormalisedFullForm { get; private set; }

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

        [GeneratedRegex(@"([\d]{4}) or ([\d]{4})")]
        private static partial Regex YearOrYear();


        [SetsRequiredMembers]
        public ParsedAgent(string original)
        {
            Original = original;

            string working = Original.TrimOuterBrackets()!;

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

                    var lowerDate = DateString.ToLower();
                    if (
                            lowerDate.StartsWith("active ")
                        || lowerDate.StartsWith("fl")
                        || lowerDate.StartsWith("florit"))
                    {
                        IsActive = true;
                    }
                    if (
                            lowerDate.StartsWith("approx")
                        || lowerDate.StartsWith("c.")
                        || lowerDate.StartsWith("circa"))
                    {
                        IsApproximate = true;
                    }

                    string yearsNormalised = DateString;
                    foreach (Match m in YearOrYear().Matches(DateString))
                    {
                        yearsNormalised = yearsNormalised.Replace(m.Value, m.Groups[1].Value);
                        IsApproximate = true;
                    }

                    StringBuilder sb = new();
                    foreach (char c in yearsNormalised)
                    {
                        if (c == '-' || char.IsDigit(c))
                        {
                            sb.Append(c);
                        }
                        if(c == '?')
                        {
                            IsApproximate = true;
                        }
                    }
                    string numericDate = sb.ToString();
                    if (numericDate.Length == 8 && numericDate.All(char.IsDigit))
                    {
                        // There was likely a non '-' year separator, so put one back in
                        numericDate = $"{numericDate[..4]}-{numericDate[4..]}";
                    }

                    // For any month/day dates captured, reduce to a year (this is v rough)
                    var parts = numericDate.Split('-');
                    numericDate = string.Join("-", parts.Select(p => new string(p.Take(4).ToArray())));
                    if(DateString.IndexOf("century", StringComparison.InvariantCultureIgnoreCase) != -1)
                    {
                        parts = numericDate.Split('-');
                        var psb = new StringBuilder();
                        for(int pi = 0; pi < parts.Length; pi++)
                        {
                            if (parts[pi].Length == 2)
                            {
                                if(pi == 0)
                                {
                                    parts[pi] = ((Convert.ToInt32(parts[pi]) - 1) * 100).ToString();
                                }
                                else
                                {
                                    parts[pi] = (Convert.ToInt32(parts[pi]) * 100 - 1).ToString();
                                }
                            }
                        }
                        numericDate = string.Join("-", parts);
                        if(numericDate.Length == 4 && numericDate.EndsWith("00"))
                        {
                            numericDate = $"{numericDate}-{Convert.ToInt32(numericDate) + 99}";
                        }
                    }

                    var twoParts = numericDate.Split("-");
                    for(int pi = 0; pi<twoParts.Length; pi++)
                    {
                        if (twoParts[pi].Length == 4)
                        {
                            if(pi == 0)
                            {
                                StartYear = Convert.ToInt32(twoParts[pi]);
                            }
                            else
                            {
                                EndYear = Convert.ToInt32(twoParts[pi]);
                            }
                        }
                    }


                    if (numericDate != DateString)
                    {
                        if (numericDate.HasText())
                        {
                            NumericDateString = numericDate;
                        }
                        else
                        {
                            // There is text in DateString, but it's not a numerical date
                            // Callers may with to see if it contains a role
                            //if(string.IsNullOrEmpty(Role))
                            //{
                            //    // Only set if we don't already have a role
                            //    Role = DateString.TrimOuterParentheses();
                            //}
                        }
                    }
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
            normSB.Append(Name);
            if (DateString.HasText())
            {
                normSB.Append(" [");
                if(IsActive)
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
                }
                else
                {
                    normSB.Append(DateString);
                }
                normSB.Append("]");
            }
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
