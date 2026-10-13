namespace LinkedArtNet;

/// <summary>
/// crm:E73 Information Object. In Linked Art 1.0 it appears only as a reference, for example the
/// specification a <see cref="DigitalObject"/> <c>conforms_to</c>.
/// </summary>
public partial class InformationObject : LinkedArtObject
{
    public InformationObject() : base(nameof(InformationObject))
    {
    }

    public InformationObject(string id) : this()
    {
        Id = id;
    }
}
