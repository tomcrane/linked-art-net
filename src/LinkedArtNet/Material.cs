namespace LinkedArtNet;

/// <summary>crm:E57 Material.</summary>
public class Material : Type
{
    public Material() : base(nameof(Material))
    {
    }

    public Material(string id, string? label) : this()
    {
        Id = id;
        Label = label;
    }
}
