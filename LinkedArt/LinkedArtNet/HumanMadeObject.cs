﻿using System.Text.Json.Serialization;

namespace LinkedArtNet;

public class HumanMadeObject : LinkedArtObject
{
    public HumanMadeObject() { Type = nameof(HumanMadeObject); }

    [JsonPropertyName("identified_by")]
    [JsonPropertyOrder(21)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? IdentifiedBy { get; set; }

    [JsonPropertyName("referred_to_by")]
    [JsonPropertyOrder(22)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? ReferredToBy { get; set; }

    [JsonPropertyName("produced_by")]
    [JsonPropertyOrder(23)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Activity>? ProducedBy { get; set; }

    [JsonPropertyName("dimension")]
    [JsonPropertyOrder(24)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Dimension>? Dimension { get; set; }

    [JsonPropertyName("shows")]
    [JsonPropertyOrder(30)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Work>? Shows { get; set; }


    [JsonPropertyName("made_of")]
    [JsonPropertyOrder(31)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? MadeOf { get; set; }


    [JsonPropertyName("part")]
    [JsonPropertyOrder(130)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual new List<HumanMadeObject>? Part { get; set; }


    [JsonPropertyName("part_of")]
    [JsonPropertyOrder(130)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual new List<HumanMadeObject>? PartOf { get; set; }


}
