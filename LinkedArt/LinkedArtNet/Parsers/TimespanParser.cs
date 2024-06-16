
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
        public LinkedArtTimeSpan? Parse (string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return null;
            }
            var s2 = s.CollapseWhiteSpace()
                      .TrimOuterBrackets()
                      .RemoveCirca()
                      .ExpandUncertainYears();

            DateTime? start = null;
            DateTime? end = null;

            // bypass the DateTimeModel for simple years
            if(s2.All(char.IsDigit) && (s2.Length == 4 || s2.Length == 3))
            {
                var year = int.Parse(s2);
                start = new DateTime(year, 1, 1);
                end = new DateTime(year+1, 1, 1); // this +1 is what the model returns, will correct later.
            }
            else
            {
                var result1 = dateTimeModel.Parse(s2);
                if (result1 != null)
                {
                    var range = result1.FirstOrDefault(r => r.TypeName == "datetimeV2.daterange");
                    if (range != null)
                    {
                        var values = ((List<Dictionary<string, string>>)range.Resolution["values"])[0];
                        try
                        {
                            start = DateTime.Parse(values.Single(kvp => kvp.Key == "start").Value);
                        }
                        catch
                        {
                            Console.WriteLine("No start");
                        }
                        try
                        {
                            end = DateTime.Parse(values.Single(kvp => kvp.Key == "end").Value);
                        }
                        catch
                        {
                            Console.WriteLine("No end");
                        }
                    }
                }
            }

            // Console.WriteLine($"{s} ==> {start} - {end}");
            if (start == null || end == null)
            {
                Console.WriteLine($"{s} ==> {start} - {end}");
            }

            return null;
        }
    }
}
