using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>crm:E5 Event: something that happened, not necessarily carried out by anyone (a flood, a fire).</summary>
[JsonDerivedType(typeof(Activity))]
public class Event : Period
{
    public Event() : base(nameof(Event))
    {
    }

    protected Event(string type) : base(type)
    {
    }
}
