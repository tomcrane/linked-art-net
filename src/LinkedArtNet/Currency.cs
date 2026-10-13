namespace LinkedArtNet;

/// <summary>crm:E98 Currency.</summary>
public class Currency : Type
{
    public Currency() : base(nameof(Currency))
    {
    }

    public Currency(string id, string? label) : this()
    {
        Id = id;
        Label = label;
    }
}
