using System.Text.Json;
using System.Text.Json.Serialization;
using LinkedArtNet.Alignment.Store;

namespace LinkedArtNet.Alignment.Import;

/// <summary>
/// The curated authority-string files of the <c>digirati-co-uk/pmc-lux</c> repository (MIT): a <c>local_string_map</c>
/// from raw strings to local identifiers, and <c>authorities</c> with those identifiers, a type, a label and the
/// authority ids that were settled by hand. They import as entities under the PMC scheme (keeping the identifiers) and
/// as overrides for any context, which the reconciler consults first (design doc 10, 7.1).
/// </summary>
public sealed record PmcLuxOverrides(
    [property: JsonPropertyName("local_string_map")] IReadOnlyDictionary<string, string> LocalStringMap,
    [property: JsonPropertyName("authorities")] IReadOnlyList<PmcLuxAuthority> Authorities)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static PmcLuxOverrides Parse(string json) => JsonSerializer.Deserialize<PmcLuxOverrides>(json, Json) ?? throw new InvalidDataException("Empty pmc-lux file.");

    public static PmcLuxOverrides Load(string path) => Parse(File.ReadAllText(path));

    /// <summary>
    /// Writes the authorities as entities (existing ids kept, equivalents from the authority ids) and the string map
    /// as overrides targeting those entities. Strings whose target has no authority record still get an override to
    /// the URI the scheme would give the identifier, with the class guessed from the file name's convention
    /// ("groups.json" is Group, "people.json" is Person) unless <paramref name="defaultClass"/> says otherwise.
    /// Re-running is harmless: entities already present are left as they are, overrides are replaced.
    /// </summary>
    public async Task<PmcLuxImportResult> ImportAsync(IAlignmentStore store, IdentifierScheme scheme, string dataset, string sourceName, EntityClass defaultClass, string? runId = null, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var entityIds = new Dictionary<string, string>(StringComparer.Ordinal);
        int entitiesAdded = 0, entitiesExisting = 0, equivalents = 0, overrides = 0, unknownTargets = 0;
        foreach (var a in Authorities)
        {
            if (string.IsNullOrWhiteSpace(a.Identifier)) continue;
            var cls = ClassOf(a.Type) ?? defaultClass;
            var id = scheme.Uri(cls, a.Identifier);
            entityIds[a.Identifier] = id;
            var existing = await store.GetEntityAsync(id, ct);
            if (existing is null)
            {
                await store.AddEntityAsync(dataset, new NewEntity(cls, a.Label, Id: id, Status: a.HasAnyAuthority ? EntityStatus.Aligned : EntityStatus.Provisional, RunId: runId), scheme, ct);
                entitiesAdded++;
            }
            else entitiesExisting++;
            foreach (var uri in a.EquivalentUris())
            {
                await store.AddEquivalentAsync(id, new Equivalent(uri, AuthorityUris.SourceOf(uri), DecisionMethod.Override, 1.0, now, RunId: runId), ct);
                equivalents++;
            }
        }
        foreach (var (raw, target) in LocalStringMap)
        {
            var normalised = StringKey.Normalise(raw);
            if (normalised.Length == 0 || string.IsNullOrWhiteSpace(target)) continue;
            if (!entityIds.TryGetValue(target, out var uri))
            {
                uri = scheme.Uri(defaultClass, target);
                unknownTargets++;
            }
            await store.PutOverrideAsync(new Override(dataset, null, normalised, uri, sourceName, $"\"{raw}\" → {target}"), ct);
            overrides++;
        }
        return new PmcLuxImportResult(entitiesAdded, entitiesExisting, equivalents, overrides, unknownTargets);
    }

    public static EntityClass? ClassOf(string? type) => type?.Trim().ToLowerInvariant() switch
    {
        "person" => EntityClass.Person,
        "group" => EntityClass.Group,
        "place" => EntityClass.Place,
        "type" or "concept" => EntityClass.Type,
        "material" => EntityClass.Material,
        "language" => EntityClass.Language,
        "period" => EntityClass.Period,
        "event" => EntityClass.Event,
        _ => null,
    };
}

public sealed record PmcLuxAuthority(
    string? Identifier, string? Type, string? Label,
    string? Ulan = null, string? Aat = null, string? Tgn = null, string? Viaf = null, string? Wikidata = null, string? Loc = null, string? Lux = null, string? Pmc = null, bool? Ignore = null)
{
    public bool HasAnyAuthority => EquivalentUris().Any();

    /// <summary>The authority URIs this record states, in the namespaces of <see cref="AuthorityUris"/>.</summary>
    public IEnumerable<string> EquivalentUris()
    {
        if (!string.IsNullOrWhiteSpace(Ulan)) yield return AuthorityUris.Ulan + Ulan.Trim();
        if (!string.IsNullOrWhiteSpace(Aat)) yield return AuthorityUris.Aat + Aat.Trim();
        if (!string.IsNullOrWhiteSpace(Tgn)) yield return AuthorityUris.Tgn + Tgn.Trim();
        if (!string.IsNullOrWhiteSpace(Viaf)) yield return AuthorityUris.Viaf + Viaf.Trim();
        if (!string.IsNullOrWhiteSpace(Wikidata)) yield return AuthorityUris.Wikidata + Wikidata.Trim();
        if (!string.IsNullOrWhiteSpace(Loc)) yield return AuthorityUris.Loc(Loc.Trim());
        if (!string.IsNullOrWhiteSpace(Lux)) yield return Lux.Trim().StartsWith("http", StringComparison.Ordinal) ? Lux.Trim() : AuthorityUris.Lux + Lux.Trim();
    }
}

public sealed record PmcLuxImportResult(int EntitiesAdded, int EntitiesExisting, int Equivalents, int Overrides, int UnknownTargets);
