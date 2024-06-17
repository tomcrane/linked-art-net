
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.DateTime;

namespace LinkedArtNet.Parsers
{
    public class TimespanParser
    {
        private DateTimeModel dateTimeModel;

        public TimespanParser()
        {
            var recognizer = new DateTimeRecognizer(Culture.English);
            dateTimeModel = recognizer.GetDateTimeModel();
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
                      .ExpandUncertainYears();

            DateTimeOffset? start = null;
            DateTimeOffset? end = null;

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
                Console.WriteLine($"{s} ==> {start} - {end}");
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
    }
}
