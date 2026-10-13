using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>crm:E22 Human-Made Object. A physical thing, or part of one.</summary>
public partial class HumanMadeObject : LinkedArtEntity, IHasDimensions
{
    public HumanMadeObject() : base(nameof(HumanMadeObject))
    {
    }

    [JsonPropertyName("produced_by")]
    [JsonPropertyOrder(23)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Production? ProducedBy { get; set; }

    [JsonPropertyName("encountered_by")]
    [JsonPropertyOrder(24)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Encounter>? EncounteredBy { get; set; }

    [JsonPropertyName("dimension")]
    [JsonPropertyOrder(25)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Dimension>? Dimension { get; set; }

    [JsonPropertyName("shows")]
    [JsonPropertyOrder(30)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<VisualItem>? Shows { get; set; }

    [JsonPropertyName("carries")]
    [JsonPropertyOrder(30)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinguisticObject>? Carries { get; set; }

    [JsonPropertyName("made_of")]
    [JsonPropertyOrder(31)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Material>? MadeOf { get; set; }

    [JsonPropertyName("part")]
    [JsonPropertyOrder(130)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<HumanMadeObject>? Part { get; set; }

    [JsonPropertyName("part_of")]
    [JsonPropertyOrder(130)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<HumanMadeObject>? PartOf { get; set; }

    [JsonPropertyName("member_of")]
    [JsonPropertyOrder(140)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Set>? MemberOf { get; set; }

    [JsonPropertyName("current_owner")]
    [JsonPropertyOrder(210)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Actor>? CurrentOwner { get; set; }

    /// <summary>la:current_permanent_custodian. A single actor on the site's examples, although the 1.0 schema declares an array.</summary>
    [JsonPropertyName("current_permanent_custodian")]
    [JsonPropertyOrder(211)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Actor? CurrentPermanentCustodian { get; set; }

    [JsonPropertyName("current_custodian")]
    [JsonPropertyOrder(212)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Actor>? CurrentCustodian { get; set; }

    [JsonPropertyName("used_for")]
    [JsonPropertyOrder(220)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Activity>? UsedFor { get; set; }

    [JsonPropertyName("current_permanent_location")]
    [JsonPropertyOrder(221)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Place? CurrentPermanentLocation { get; set; }

    [JsonPropertyName("current_location")]
    [JsonPropertyOrder(222)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Place? CurrentLocation { get; set; }

    [JsonPropertyName("destroyed_by")]
    [JsonPropertyOrder(225)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Destruction? DestroyedBy { get; set; }

    [JsonPropertyName("removed_by")]
    [JsonPropertyOrder(230)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<PartRemoval>? RemovedBy { get; set; }

    [JsonPropertyName("changed_ownership_through")]
    [JsonPropertyOrder(251)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Acquisition>? ChangedOwnershipThrough { get; set; }

    [JsonPropertyName("modified_by")]
    [JsonPropertyOrder(820)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Modification>? ModifiedBy { get; set; }

    [JsonPropertyName("held_or_supported_by")]
    [JsonPropertyOrder(1021)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<HumanMadeObject>? HeldOrSupportedBy { get; set; }
}
