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

        //var dt = DateTime.ParseExact(date, LinkedArtDate.Format, CultureInfo.InvariantCulture);
        //return new LinkedArtDate(dt);

        var dto = ParseIso8601(date);
        return new LinkedArtDate(dto);
    }

    public override void Write(Utf8JsonWriter writer, LinkedArtDate value, JsonSerializerOptions options)
    {
        if (value.DtOffset.HasValue)
        {
            writer.WriteStringValue(FormatIso8601(value.DtOffset.Value, value.serializeWithTimezone));
        }
        else
        {
            writer.WriteStringValue(DayFormat(value.Year, value.Month, value.Day));
        }
    }

    public static string DayFormat(int year, int month, int day)
    {
        return string.Format("{0:D4}-{1:D2}-{2:D2}T00:00:00", year, month, day);
    }

    public static string FormatIso8601(DateTimeOffset dto, bool serializeWithTimezone = true)
    {
        string format;

        if (serializeWithTimezone)
        {
            format = dto.Offset == TimeSpan.Zero
                ? "yyyy-MM-ddTHH:mm:ssZ"
                : "yyyy-MM-ddTHH:mm:sszzz";
        }
        else
        {
            format = "yyyy-MM-ddTHH:mm:ss";
        }

        return dto.ToString(format, CultureInfo.InvariantCulture);
    }

    private static readonly string[] parseFormats = [
        "yyyy-MM-dd'T'HH:mm:ss.FFF",
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd'T'HH:mm:ss.FFFK",
        "yyyy-MM-dd'T'HH:mm:ssK"
    ];

    public static DateTimeOffset ParseIso8601(string iso8601String)
    {
        return DateTimeOffset.ParseExact(
            iso8601String,
            parseFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None);
    }
}