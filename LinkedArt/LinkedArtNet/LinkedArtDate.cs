using System.Text.Json.Serialization;

namespace LinkedArtNet;

[JsonConverter(typeof(LinkedArtDateConverter))]
public class LinkedArtDate
{
    // TODO: This is not the correct datetime format
    //public const string Format = "yyyy-MM-dd\\THH:mm:ssZ"; 
    public const string Format = "o";

    //public DateTime? Date;
    public DateTimeOffset? DtOffset;
    public int Year, Month, Day;
    public bool serializeWithTimezone = false;

    public LinkedArtDate(DateTimeOffset dto, bool serializeWithTimezone = true)
    {
        // https://linked.art/example/object/spring/4.json does not have timezone info
        DtOffset = dto;
        Year = dto.Year;
        Month = dto.Month;
        Day = dto.Day;
        this.serializeWithTimezone = serializeWithTimezone;
    }

    public LinkedArtDate(int year, int month, int day, bool serializeWithTimezone = true)
    {
        Year = year;
        Month = month;
        Day = day;
        if(year >= 0)
        {
            DtOffset = new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero);
        }
        this.serializeWithTimezone = serializeWithTimezone;
    }
}
