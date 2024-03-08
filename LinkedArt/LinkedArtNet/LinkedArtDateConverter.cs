using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LinkedArtNet;

public class LinkedArtDateConverter : JsonConverter<LinkedArtDate>
{
    public override LinkedArtDate? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var date = reader.GetString()!;
        // separator is also -ve year
        var parts = date.Split('-', StringSplitOptions.RemoveEmptyEntries);
        var year = int.Parse(parts[0]);
        if (date.StartsWith('-')) year = 0 - year;
        if(year < 0)
        {
            return new LinkedArtDate(year, int.Parse(parts[1]), int.Parse(parts[2]));
        }
        var dt = DateTime.ParseExact(date, LinkedArtDate.Format, CultureInfo.InvariantCulture);
        return new LinkedArtDate(dt);
    }

    public override void Write(Utf8JsonWriter writer, LinkedArtDate value, JsonSerializerOptions options)
    {
        if (value.Date.HasValue)
        {
            writer.WriteStringValue(value.Date.Value.ToString(LinkedArtDate.Format));
        }
        else
        {
            var s = string.Format("{0:D4}-{1:D2}-{2:D2} 00:00:00", value.Year, value.Month, value.Day);
            writer.WriteStringValue(s);
        }
    }
}