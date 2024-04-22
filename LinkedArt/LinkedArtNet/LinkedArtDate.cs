using System.Text.Json.Serialization;

namespace LinkedArtNet;

[JsonConverter(typeof(LinkedArtDateConverter))]
public class LinkedArtDate
{
    // TODO: This is not the correct datetime format
    public const string Format = "yyyy-MM-dd HH:mm:ss";
 
    public DateTime? Date;
    public int Year, Month, Day;

    public LinkedArtDate(DateTime dt)
    {
        Date = dt;
        Year = dt.Year;
        Month = dt.Month;
        Day = dt.Day;
    }

    public LinkedArtDate(int year, int month, int day)
    {
        Year = year;
        Month = month;
        Day = day;
        if(year >= 0)
        {
            Date = new DateTime(year, month, day, 0, 0, 0);
        }
    }
}
