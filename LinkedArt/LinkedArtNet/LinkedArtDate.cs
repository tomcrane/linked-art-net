using System.Text.Json.Serialization;

namespace LinkedArtNet;

[JsonConverter(typeof(LinkedArtDateConverter))]
public class LinkedArtDate
{
    public const string Format = "yyyy-MM-dd hh:mm:ss";
 
    public DateTime? Date;
    public int Year, Month, Day;

    public LinkedArtDate(DateTime dt)
    {
        Date = dt;
    }

    public LinkedArtDate(int year, int month, int day)
    {
        Year = year;
        Month = month;
        Day = day;
        if(year >= 0)
        {
            Date = new DateTime(year, month, day);
        }
    }
}
