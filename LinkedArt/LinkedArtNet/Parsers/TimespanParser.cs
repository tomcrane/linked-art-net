using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.DateTime;
using System.Text;
using System.Text.RegularExpressions;

namespace LinkedArtNet.Parsers
{
    public partial class TimespanParser
    {
        private DateTimeModel dateTimeModel;

        public TimespanParser()
        {
            var recognizer = new DateTimeRecognizer(Culture.English);
            dateTimeModel = recognizer.GetDateTimeModel();
        }

        [GeneratedRegex(@"[\d]{4}")]
        private static partial Regex FourDigitYear();

        [GeneratedRegex(@"([\d]{4}) or ([\d]{4})")]
        private static partial Regex YearOrYear();

        public Tuple<LinkedArtTimeSpan?, LinkedArtTimeSpan?, TimespanParserHints>? ParseSimpleYearDateRange(string? s)
        {
            // From Archive Authority <Dates> field
            if (string.IsNullOrEmpty(s))
            {
                return null;
            }
            var hints = new TimespanParserHints();
            if(s.Contains("active") || s.Contains("florit") || s.StartsWith("fl")) 
            {
                hints.IsDatesActive = true;
            }
            // variants of 1930-2010
            // first pass, just find the first and last whole years; come back for more refinement later
            var parsedYears = FourDigitYear().Matches(s).Skip(1).Select(m => Convert.ToInt32(m.Value)).ToList();
            if(parsedYears.Count > 1)
            {
                return new(
                    LinkedArtTimeSpan.FromYear(parsedYears[0]),
                    LinkedArtTimeSpan.FromYear(parsedYears[^1]),
                    hints);
            }
            if(parsedYears.Count == 1)
            {
                return new(
                    LinkedArtTimeSpan.FromYear(parsedYears[0]),
                    null,
                    hints);
            }
            return null;
        }

        // TODO - bring logic in from UAL date parser and other sources
        public LinkedArtTimeSpan? Parse(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return null;
            }
            var s2 = s.CollapseWhiteSpace()
                      .TrimOuterBrackets()
                      .RemoveCirca()
                      .ExpandUncertainYears()?
                      .Trim();

            DateTimeOffset? start = null;
            DateTimeOffset? end = null;
            bool wasParsedByLib = false;

            if (string.IsNullOrWhiteSpace(s2))
            {
                return null;
            }

            // bypass the DateTimeModel for simple years
            if (s2.All(char.IsDigit) && (s2.Length == 4 || s2.Length == 3))
            {
                var year = int.Parse(s2);
                start = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
                end = new DateTimeOffset(year + 1, 1, 1, 0, 0, 0, TimeSpan.Zero); // this +1 is what the model returns, will correct later.
            }
            else
            {
                var result1 = dateTimeModel.Parse(s2);
                wasParsedByLib = true;
                if (result1 != null)
                {
                    // TODO: We may get multiple results, multiple date ranges.
                    // So we should walk through them and take the min and max as the timespan

                    // TODO: look for TIMEX expressions which will be produced from strings like "Winter 1945"
                    // Convert to start and end dates

                    var range = result1.FirstOrDefault(r => r.TypeName == "datetimeV2.daterange");
                    if (range != null)
                    {
                        var values = ((List<Dictionary<string, string>>)range.Resolution["values"])[0];                        
                        try
                        {
                            start = DateTimeOffset.Parse(values.Single(kvp => kvp.Key == "start").Value);
                        }
                        catch
                        {
                            //Console.WriteLine("No start");
                        }
                        try
                        {
                            end = DateTimeOffset.Parse(values.Single(kvp => kvp.Key == "end").Value);
                            bool isSingleYear = false;
                            if(values.TryGetValue("timex", out string? timex))
                            {
                                if(timex != null && timex.Length == 4 && timex.All(char.IsDigit))
                                {
                                    isSingleYear = true;
                                }
                            }
                            // The way the library parses dates it will give
                            // "1971" =>      1/1/1971-1/1/1972, 
                            // "1971-1972" => 1/1/1971-1/1/1972, 
                            // But we can tell what happened by looking at the timex value
                            if (
                                !isSingleYear && 
                                start != null && 
                                start.Value.Year < end.Value.Year && 
                                end.IsStartOfYear()
                            )
                            {
                                end = new DateTimeOffset(end.Value.Year + 1, 1, 1, 0, 0, 0, TimeSpan.Zero);
                            }
                        }
                        catch
                        {
                            //Console.WriteLine("No end");
                        }
                    }
                }
            }

            // Console.WriteLine($"{s} ==> {start} - {end}");
            if (start == null || end == null)
            {
                // Console.WriteLine($"{s} ==> {start} - {end}");
            }

            // In this first implementation, we now have a start, and end, or both.
            // I think we shouldn't be using a TimeSpan for open-ended dates but come back to that.
            // (or open-start dates)
            var ts = new LinkedArtTimeSpan()
            {
                Label = s, // do we want to do this?
                BeginOfTheBegin = GetBegin(start, end),
                EndOfTheEnd = GetEnd(start, end)
            };

            //if(wasParsedByLib)
            //{
                Console.WriteLine($"*** date {s} => {s2} => {ts.BeginOfTheBegin?.DtOffset} - {ts.EndOfTheEnd?.DtOffset}");
            //}
            return ts;
        }

        private LinkedArtDate? GetBegin(DateTimeOffset? start, DateTimeOffset? end)
        {
            if (start.HasValue)
            {
                return new LinkedArtDate(start.Value);
            }
            // we only have an end
            if (end.HasValue)
            {
                return new LinkedArtDate(end.Value).LastSecondOfDay();
            }
            return null;
        }
        private LinkedArtDate? GetEnd(DateTimeOffset? start, DateTimeOffset? end)
        {
            if (end.HasValue)
            {
                if (end.Value.TimeOfDay == TimeSpan.Zero)
                {
                    //if(start.HasValue && start.Value.Year != end.Value.Year)
                    //{
                    //    // The Parser will turn 1989 into 1989,1,1 - 1990,1,1
                    //    // But will turn 1989-1991 into 1989,1,1 - 1991,1,1 
                    //    return new LinkedArtDate(end.Value.AddYears(1).AddSeconds(-1));
                    //}
                    return new LinkedArtDate(end.Value.AddSeconds(-1));
                }
                return new LinkedArtDate(end.Value);
            }
            // we only have a start...
            if (start.HasValue)
            {
                return new LinkedArtDate(start.Value.Year, start.Value.Month, start.Value.Day).LastSecondOfDay();
            }

            return null;
        }


        public static TimespanParserHints ParseLibraryAgentDate(string dateString)
        {
            var tsHints = new TimespanParserHints { DateString = dateString };

            var lowerDate = tsHints.DateString.ToLower();
            if (
                    lowerDate.StartsWith("active ")
                || lowerDate.StartsWith("fl")
                || lowerDate.StartsWith("florit"))
            {
                tsHints.IsDatesActive = true;
            }
            if (
                    lowerDate.StartsWith("approx")
                || lowerDate.StartsWith("c.")
                || lowerDate.StartsWith("circa"))
            {
                tsHints.IsCirca = true;
            }

            string yearsNormalised = tsHints.DateString;
            foreach (Match m in YearOrYear().Matches(tsHints.DateString))
            {
                yearsNormalised = yearsNormalised.Replace(m.Value, m.Groups[1].Value);
                tsHints.IsCirca= true;
            }

            StringBuilder sb = new();
            foreach (char c in yearsNormalised)
            {
                if (c == '-' || char.IsDigit(c))
                {
                    sb.Append(c);
                }
                if (c == '?')
                {
                    tsHints.IsCirca = true;
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
            if (tsHints.DateString.IndexOf("century", StringComparison.InvariantCultureIgnoreCase) != -1)
            {
                parts = numericDate.Split('-');
                var psb = new StringBuilder();
                for (int pi = 0; pi < parts.Length; pi++)
                {
                    if (parts[pi].Length == 2)
                    {
                        if (pi == 0)
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
                if (numericDate.Length == 4 && numericDate.EndsWith("00"))
                {
                    numericDate = $"{numericDate}-{Convert.ToInt32(numericDate) + 99}";
                }
            }

            var twoParts = numericDate.Split("-");
            for (int pi = 0; pi < twoParts.Length; pi++)
            {
                if (twoParts[pi].Length == 4)
                {
                    if (pi == 0)
                    {
                        tsHints.StartYear = Convert.ToInt32(twoParts[pi]);
                    }
                    else
                    {
                        tsHints.EndYear = Convert.ToInt32(twoParts[pi]);
                    }
                }
            }


            if (numericDate != tsHints.DateString)
            {
                if (!string.IsNullOrWhiteSpace(numericDate))
                {
                    tsHints.NumericDateString = numericDate;
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

            return tsHints;

        }

    }
}
