using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using LinkedArtNet.Alignment.Store;

namespace LinkedArtNet.Alignment.Authorities;

/// <summary>What a GET of a public URL came back with: the body on a 200, otherwise the status or the failure, so the message can say which (the owner, 2026-11-08: "is that no response, or a specific HTTP error code?").</summary>
public sealed record WebAnswer(string? Body, int Status, string? Failure = null)
{
    public bool Ok => Body is not null;
    /// <summary>"answered 404 Not Found", or "did not answer: timed out".</summary>
    public string Said => Failure is not null ? $"did not answer: {Failure}" : $"answered {Status}{(Enum.IsDefined(typeof(HttpStatusCode), Status) ? " " + Regex.Replace(((HttpStatusCode)Status).ToString(), "(?<=[a-z])(?=[A-Z])", " ") : "")}";
    public static WebAnswer Of(string body) => new(body, 200);
    public static WebAnswer NotFound => new(null, 404);
}

/// <summary>A GET of a public URL; one implementation over <see cref="HttpClient"/>, another canned for tests.</summary>
public interface IWebFetch
{
    Task<WebAnswer> GetAsync(string url, string accept, CancellationToken ct = default);
}

/// <summary>
/// The polite client: one identifying User-Agent (Wikimedia's API policy asks for one), a short timeout, and null for anything
/// but a 200. Nothing is retried; the person presses "Pull in" again.
/// </summary>
public sealed class HttpWebFetch(HttpClient http) : IWebFetch
{
    public const string UserAgent = "linked-art-net alignment workbench (+https://github.com/tomcrane/linked-art-net)";

    public async Task<WebAnswer> GetAsync(string url, string accept, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            request.Headers.TryAddWithoutValidation("Accept", accept);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(20));
            using var response = await http.SendAsync(request, cts.Token);
            if (!response.IsSuccessStatusCode) return new WebAnswer(null, (int)response.StatusCode);
            return WebAnswer.Of(await response.Content.ReadAsStringAsync(cts.Token));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new WebAnswer(null, 0, "timed out after 20 seconds"); }
        catch (HttpRequestException e) { return new WebAnswer(null, 0, e.Message); }
    }
}

/// <summary>A picture on Wikimedia Commons: the file, never its bytes, with what its licence asks to be shown beside it.</summary>
public sealed record Picture(string FileName, string FilePage, string? ThumbUrl, string? Licence, string? LicenceUrl, string? Author, string? Credit, bool AttributionRequired)
{
    /// <summary>The line shown under the thumbnail, which Commons licences require: author and licence, the credit when there is no author.</summary>
    public string Attribution => string.Join(", ", new[] { Author ?? Credit, Licence }.Where(s => !string.IsNullOrWhiteSpace(s))!) is { Length: > 0 } s ? s : "Wikimedia Commons";
}

/// <summary>One fact a source states, with a link when the fact is a page.</summary>
public sealed record Fact(string Key, string Value, string? Url = null);

/// <summary>
/// What one aligned source says about an entity, fetched live and cached on the record's row (plan section 7, "From the sources, on
/// demand", the owner, 2026-11-08). Every field is the source's own, dated by <see cref="At"/>; nothing here is ours. A LUX record that
/// carries one of our identifiers is a <see cref="SelfEcho"/>: our earlier export reflected back, not a second witness (doc 10 7.4).
/// </summary>
public sealed record Pulled(
    string Uri, string Source, DateTimeOffset At, string? Label, string? Description, IReadOnlyList<string> Aliases, IReadOnlyList<Fact> Facts,
    string? Extract = null, string? ExtractFrom = null, string? ExtractUrl = null, Picture? Picture = null, double? Lat = null, double? Long = null,
    bool SelfEcho = false, string? Note = null, string? Error = null)
{
    public bool HasAnything => Label is not null || Description is not null || Aliases.Count > 0 || Facts.Count > 0 || Extract is not null || Picture is not null || Lat is not null;
}

/// <summary>
/// Pulls what the aligned sources say about an entity, on demand, and caches it under <c>pulled</c> in the record's <c>data</c>
/// (<see cref="IAlignmentStore.PutPulledAsync"/>); the page reads the cache (<see cref="Read"/>). Fetchers: Wikidata's entity JSON,
/// then the English Wikipedia summary for its sitelink and Commons' metadata for its image; ULAN through Getty's SPARQL endpoint
/// (the vocabulary's own JSON endpoint failed on 2026-11-08 with a certificate error on their side); LoC's JSON-LD; LUX's Linked Art
/// record. Nothing is fetched on page load or inside a run. Shapes checked live on 2026-11-08 against John Robert Cozens (Q613852,
/// ULAN 500000438, LoC n80153730) and LUX's "King's Gallery" and "Lexington" records.
/// </summary>
public sealed class SourcePull(IAlignmentStore store, IWebFetch web, IdentifierScheme? scheme = null)
{
    public const string Key = "pulled";
    /// <summary>Where a record's source citations from its bulk file are kept in its data.</summary>
    public const string SourcesKey = "sources";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    /// <summary>The cached pull on a record, if any.</summary>
    public static Pulled? Read(AuthorityRecord? record)
    {
        if (record?.DataJson is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(record.DataJson);
            if (doc.RootElement.TryGetProperty(Key, out var p)) return p.Deserialize<Pulled>(JsonOptions);
            // not pulled, but its source citations were read from its bulk file (LocCitationsLoader): they are what a pull is mostly for
            if (doc.RootElement.TryGetProperty(SourcesKey, out var s) && s.ValueKind == JsonValueKind.Array && s.GetArrayLength() > 0)
                return new Pulled(record.Uri, record.Source, record.FetchedAt, record.Label, null, [], s.EnumerateArray().Select(x => x.GetString()).OfType<string>().Select(x => new Fact("Found in", x)).ToList(),
                    Note: "From the source's bulk file, not asked of the source itself.");
            return null;
        }
        catch (JsonException) { return null; }
    }

    /// <summary>Whether a source of this URI can be pulled at all.</summary>
    public static bool CanPull(string uri) => AuthorityUris.SourceOf(uri) is "wikidata" or "ulan" or "loc" or "lux";

    /// <summary>Pulls every identifier of the entity that has a fetcher, in order, a dozen at most; each result cached when it fetched.</summary>
    public async Task<IReadOnlyList<Pulled>> PullAllAsync(Entity entity, CancellationToken ct = default)
    {
        var list = new List<Pulled>();
        foreach (var q in entity.Equivalents.Where(q => CanPull(q.Uri)).Take(12))
            list.Add(await PullAsync(q.Uri, ct));
        return list;
    }

    /// <summary>Pulls one identifier: fetched, read, cached. A failed fetch comes back with <see cref="Pulled.Error"/> set and is not cached.</summary>
    public async Task<Pulled> PullAsync(string uri, CancellationToken ct = default)
    {
        var source = AuthorityUris.SourceOf(uri);
        var now = DateTimeOffset.UtcNow;
        Pulled result;
        try
        {
            result = source switch
            {
                "wikidata" => await WikidataAsync(uri, now, ct),
                "ulan" => await UlanAsync(uri, now, ct),
                "loc" => await LocAsync(uri, now, ct),
                "lux" => await LuxAsync(uri, now, ct),
                _ => new Pulled(uri, source, now, null, null, [], [], Error: "No fetcher for this source."),
            };
        }
        // a source that answers with nothing, or with something that is not the record expected, is a pull that failed and not a
        // page that fell over (the owner, 2026-11-22: LUX answered a Charterhouse identifier with an empty body, and the entity's
        // page showed the exception)
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            result = new Pulled(uri, source, now, null, null, [], [], Error: $"The source answered, but not with a record that could be read ({e.GetType().Name}).");
        }
        if (result.Error is null)
            await store.PutPulledAsync(uri, source, result.Label, JsonSerializer.Serialize(result, JsonOptions), ct);
        return result;
    }

    private static Pulled Failed(string uri, string source, DateTimeOffset now, string what, WebAnswer? answer = null) =>
        new(uri, source, now, null, null, [], [], Error: answer is null ? $"{what} answered with nothing readable." : $"{what} {answer.Said}."
            + (answer.Status == 404 && source == "lux" ? " LUX has re-minted its identifiers before (doc 10 7.5): the record may exist under another." : ""));

    // ---- Wikidata, Wikipedia, Commons ----

    private async Task<Pulled> WikidataAsync(string uri, DateTimeOffset now, CancellationToken ct)
    {
        var qid = uri.Split('/').Last();
        var answer = await web.GetAsync($"https://www.wikidata.org/wiki/Special:EntityData/{qid}.json", "application/json", ct);
        if (answer.Body is not { } json) return Failed(uri, "wikidata", now, "Wikidata", answer);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("entities", out var entities) || !entities.TryGetProperty(qid, out var e))
            return Failed(uri, "wikidata", now, "Wikidata");
        var label = Lang(e, "labels", "en");
        var description = Lang(e, "descriptions", "en");
        var aliases = new List<string>();
        if (e.TryGetProperty("aliases", out var al) && al.TryGetProperty("en", out var en))
            foreach (var a in en.EnumerateArray()) if (a.TryGetProperty("value", out var v) && v.GetString() is { } s) aliases.Add(s);
        var facts = new List<Fact>();
        e.TryGetProperty("claims", out var claims);
        if (WikidataTime(claims, "P569") is { } born) facts.Add(new Fact("Born", born));
        if (WikidataTime(claims, "P570") is { } died) facts.Add(new Fact("Died", died));
        if (WikidataTime(claims, "P571") is { } founded) facts.Add(new Fact("Founded", founded));
        if (WikidataTime(claims, "P576") is { } dissolved) facts.Add(new Fact("Dissolved", dissolved));
        var ids = new List<string>();
        foreach (var (prop, name, prefix) in new[] { ("P214", "VIAF", AuthorityUris.Viaf), ("P245", "ULAN", AuthorityUris.Ulan), ("P244", "LoC", AuthorityUris.LocNames), ("P1667", "TGN", AuthorityUris.Tgn), ("P1014", "AAT", AuthorityUris.Aat), ("P227", "GND", AuthorityUris.Gnd) })
            foreach (var id in WikidataStrings(claims, prop).Take(2)) { ids.Add($"{name} {id}"); facts.Add(new Fact(name, id, prefix + id)); }
        double? lat = null, lon = null;
        if (claims.ValueKind == JsonValueKind.Object && claims.TryGetProperty("P625", out var p625))
            foreach (var c in p625.EnumerateArray())
                if (Value(c) is { } coord && coord.TryGetProperty("latitude", out var la) && coord.TryGetProperty("longitude", out var lo)) { lat = la.GetDouble(); lon = lo.GetDouble(); break; }
        string? extract = null, extractUrl = null, imageFile = WikidataStrings(claims, "P18").FirstOrDefault();
        if (e.TryGetProperty("sitelinks", out var sl) && sl.TryGetProperty("enwiki", out var enwiki) && enwiki.TryGetProperty("title", out var t) && t.GetString() is { } title)
        {
            var summary = (await web.GetAsync("https://en.wikipedia.org/api/rest_v1/page/summary/" + Uri.EscapeDataString(title.Replace(' ', '_')), "application/json", ct)).Body;
            if (summary is not null)
            {
                using var wp = JsonDocument.Parse(summary);
                var r = wp.RootElement;
                extract = Str(r, "extract");
                extractUrl = r.TryGetProperty("content_urls", out var cu) && cu.TryGetProperty("desktop", out var d) ? Str(d, "page") : null;
                extractUrl ??= "https://en.wikipedia.org/wiki/" + Uri.EscapeDataString(title.Replace(' ', '_'));
                if (imageFile is null && r.TryGetProperty("originalimage", out var oi) && Str(oi, "source") is { } src)
                    imageFile = WebUtility.UrlDecode(src.Split('/').Last());
            }
        }
        var picture = imageFile is null ? null : await CommonsAsync(imageFile, ct);
        return new Pulled(uri, "wikidata", now, label, description, aliases, facts, extract, extract is null ? null : "English Wikipedia", extractUrl, picture, lat, lon);
    }

    /// <summary>Commons' metadata for a file: the thumbnail URL, the file page, the licence and author the licence asks to be shown. Null when Commons has no such file.</summary>
    public async Task<Picture?> CommonsAsync(string fileName, CancellationToken ct)
    {
        var url = "https://commons.wikimedia.org/w/api.php?action=query&prop=imageinfo&iiprop=extmetadata%7Curl&iiurlwidth=240&format=json&titles=" + Uri.EscapeDataString("File:" + fileName);
        if ((await web.GetAsync(url, "application/json", ct)).Body is not { } json) return null;
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("query", out var q) || !q.TryGetProperty("pages", out var pages)) return null;
        foreach (var page in pages.EnumerateObject())
        {
            if (!page.Value.TryGetProperty("imageinfo", out var infos) || infos.GetArrayLength() == 0) continue;
            var ii = infos[0];
            ii.TryGetProperty("extmetadata", out var em);
            string? Meta(string key) => em.ValueKind == JsonValueKind.Object && em.TryGetProperty(key, out var m) ? StripHtml(Str(m, "value")) : null;
            var required = !string.Equals(Meta("AttributionRequired"), "false", StringComparison.OrdinalIgnoreCase);
            return new Picture(fileName, Str(ii, "descriptionurl") ?? "https://commons.wikimedia.org/wiki/File:" + Uri.EscapeDataString(fileName),
                Str(ii, "thumburl"), Meta("LicenseShortName"), Meta("LicenseUrl"), CleanAuthor(Meta("Artist")), Meta("Credit"), required);
        }
        return null;
    }

    /// <summary>Commons' author field as a name: its boilerplate for an unstated author ("No machine-readable author provided. Ragesoss assumed (based on copyright claims).") becomes "Ragesoss (assumed)"; a long field is cut.</summary>
    public static string? CleanAuthor(string? author)
    {
        if (author is null) return null;
        var m = Regex.Match(author, @"No machine-readable author provided\.\s*(.+?)\s+assumed\b", RegexOptions.IgnoreCase);
        if (m.Success) author = m.Groups[1].Value + " (assumed)";
        author = author.Trim().TrimEnd('.');
        return author.Length > 60 ? author[..57].TrimEnd() + "..." : author;
    }

    private static string? Lang(JsonElement e, string prop, string lang) =>
        e.TryGetProperty(prop, out var p) && p.TryGetProperty(lang, out var l) ? Str(l, "value") : null;

    private static JsonElement? Value(JsonElement claim) =>
        claim.TryGetProperty("mainsnak", out var snak) && snak.TryGetProperty("datavalue", out var dv) && dv.TryGetProperty("value", out var v) ? v : null;

    private static IEnumerable<string> WikidataStrings(JsonElement claims, string prop)
    {
        if (claims.ValueKind != JsonValueKind.Object || !claims.TryGetProperty(prop, out var list)) yield break;
        foreach (var c in list.EnumerateArray())
            if (Value(c) is { ValueKind: JsonValueKind.String } v && v.GetString() is { Length: > 0 } s) yield return s;
    }

    /// <summary>A Wikidata time at its own precision: "+1752-00-00T00:00:00Z" at precision 9 is "1752", at 11 a day.</summary>
    public static string? WikidataTime(JsonElement claims, string prop)
    {
        if (claims.ValueKind != JsonValueKind.Object || !claims.TryGetProperty(prop, out var list)) return null;
        foreach (var c in list.EnumerateArray())
        {
            if (Value(c) is not { ValueKind: JsonValueKind.Object } v || Str(v, "time") is not { } time) continue;
            var precision = v.TryGetProperty("precision", out var p) ? p.GetInt32() : 9;
            var m = Regex.Match(time, @"^([+-])(\d+)-(\d\d)-(\d\d)T");
            if (!m.Success) return time;
            var year = m.Groups[2].Value.TrimStart('0') is { Length: > 0 } y ? y : "0";
            var bc = m.Groups[1].Value == "-" ? " BC" : "";
            return precision >= 11 ? $"{year}-{m.Groups[3].Value}-{m.Groups[4].Value}{bc}" : precision == 10 ? $"{year}-{m.Groups[3].Value}{bc}" : year + bc;
        }
        return null;
    }

    // ---- ULAN ----

    private async Task<Pulled> UlanAsync(string uri, DateTimeOffset now, CancellationToken ct)
    {
        var query = $"select ?label ?bio ?start ?end ?nat ?gender ?note where {{ <{uri}> gvp:prefLabelGVP/gvp:term ?label . optional {{ <{uri}> foaf:focus ?a . optional {{ ?a gvp:biographyPreferred ?b . ?b schema:description ?bio . optional {{ ?b gvp:estStart ?start }} optional {{ ?b gvp:estEnd ?end }} optional {{ ?b schema:gender/gvp:prefLabelGVP/gvp:term ?gender }} }} optional {{ ?a gvp:nationalityPreferred ?n . ?n gvp:prefLabelGVP/gvp:term ?nat }} }} optional {{ <{uri}> skos:scopeNote ?sn . ?sn rdf:value ?note }} }} limit 1";
        var answer = await web.GetAsync("https://vocab.getty.edu/sparql.json?query=" + Uri.EscapeDataString(query), "application/sparql-results+json", ct);
        if (answer.Body is not { } json) return Failed(uri, "ulan", now, "Getty's SPARQL endpoint", answer);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("results", out var results) || !results.TryGetProperty("bindings", out var bindings) || bindings.GetArrayLength() == 0)
            return new Pulled(uri, "ulan", now, null, null, [], [], Note: "ULAN has no record at this URI.");
        var b = bindings[0];
        string? Bound(string name) => b.TryGetProperty(name, out var v) ? Str(v, "value") : null;
        var facts = new List<Fact>();
        if (Bound("nat") is { } nat) facts.Add(new Fact("Nationality", nat));
        if (Bound("start") is { } start) facts.Add(new Fact("Dates", start + (Bound("end") is { } end ? $" to {end}" : "")));
        if (Bound("gender") is { } gender) facts.Add(new Fact("Gender", gender));
        if (Bound("note") is { } note) facts.Add(new Fact("Note", note));
        facts.Add(new Fact("Full record", "Getty's display", $"https://www.getty.edu/vow/ULANFullDisplay?find=&role=&nation=&subjectid={uri.Split('/').Last()}"));
        return new Pulled(uri, "ulan", now, Bound("label"), Bound("bio"), [], facts);
    }

    // ---- Library of Congress ----

    private async Task<Pulled> LocAsync(string uri, DateTimeOffset now, CancellationToken ct)
    {
        var answer = await web.GetAsync(uri.Replace("http://", "https://") + ".json", "application/json", ct);
        if (answer.Body is not { } json) return Failed(uri, "loc", now, "id.loc.gov", answer);
        const string Mads = "http://www.loc.gov/mads/rdf/v1#";
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return Failed(uri, "loc", now, "id.loc.gov");
        string? label = null;
        var aliases = new List<string>();
        var facts = new List<Fact>();
        foreach (var node in doc.RootElement.EnumerateArray())
        {
            var id = Str(node, "@id");
            var types = node.TryGetProperty("@type", out var t) && t.ValueKind == JsonValueKind.Array ? t.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : [];
            if (id == uri || id == uri.Replace("http://", "https://"))
            {
                label = First(node, Mads + "authoritativeLabel");
                foreach (var ext in Ids(node, Mads + "hasExactExternalAuthority").Concat(Ids(node, "http://www.w3.org/2004/02/skos/core#exactMatch")).Distinct().Take(6))
                    facts.Add(new Fact("Same as", ext, ext));
            }
            else if (types.Any(x => x.EndsWith("Variant", StringComparison.Ordinal)) && First(node, Mads + "variantLabel") is { } variant) aliases.Add(variant);
            else if (types.Any(x => x.EndsWith("#Source", StringComparison.Ordinal)))
            {
                var source = First(node, Mads + "citationSource");
                var note = First(node, Mads + "citationNote");
                if (source is not null || note is not null) facts.Add(new Fact("Found in", string.Join(" ", new[] { source, note }.Where(s => !string.IsNullOrWhiteSpace(s)))));
            }
            else if (types.Any(x => x.EndsWith("#RWO", StringComparison.Ordinal)))
            {
                if (First(node, Mads + "birthDate") is { } born) facts.Add(new Fact("Born", born));
                if (First(node, Mads + "deathDate") is { } died) facts.Add(new Fact("Died", died));
                foreach (var occ in Labels(node, Mads + "occupation").Take(4)) facts.Add(new Fact("Occupation", occ));
                foreach (var place in Labels(node, Mads + "associatedLocale").Take(3)) facts.Add(new Fact("Place", place));
                if (First(node, Mads + "fullerName") is { } fuller) facts.Add(new Fact("Fuller name", fuller));
            }
        }
        if (label is null) return new Pulled(uri, "loc", now, null, null, [], [], Note: "id.loc.gov has no authority at this URI.");
        return new Pulled(uri, "loc", now, label, null, aliases.Distinct().ToList(), facts);

        static string? First(JsonElement node, string prop)
        {
            if (!node.TryGetProperty(prop, out var list) || list.ValueKind != JsonValueKind.Array) return null;
            foreach (var v in list.EnumerateArray()) if (Str(v, "@value") is { } s) return s;
            return null;
        }
        static IEnumerable<string> Ids(JsonElement node, string prop)
        {
            if (!node.TryGetProperty(prop, out var list) || list.ValueKind != JsonValueKind.Array) yield break;
            foreach (var v in list.EnumerateArray()) if (Str(v, "@id") is { } s && !s.StartsWith("_:", StringComparison.Ordinal)) yield return s;
        }
        static IEnumerable<string> Labels(JsonElement node, string prop)
        {
            if (!node.TryGetProperty(prop, out var list) || list.ValueKind != JsonValueKind.Array) yield break;
            foreach (var v in list.EnumerateArray())
            {
                if (Str(v, "@value") is { } s) yield return s;
                else if (v.TryGetProperty("http://www.w3.org/2000/01/rdf-schema#label", out var l) && l.ValueKind == JsonValueKind.Array && l.GetArrayLength() > 0 && Str(l[0], "@value") is { } ls) yield return ls;
            }
        }
    }

    // ---- LUX ----

    private async Task<Pulled> LuxAsync(string uri, DateTimeOffset now, CancellationToken ct)
    {
        var answer = await web.GetAsync(uri, "application/json", ct);
        if (answer.Body is not { } json) return Failed(uri, "lux", now, "LUX", answer);
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        var label = Str(r, "_label");
        var names = new List<string>();
        if (r.TryGetProperty("identified_by", out var ids))
            foreach (var n in ids.EnumerateArray())
                if (Str(n, "type") == "Name" && Str(n, "content") is { } c && c != label && EnglishOrUntagged(n)) names.Add(c);
        var facts = new List<Fact>();
        string? description = null;
        if (r.TryGetProperty("referred_to_by", out var statements))
            foreach (var s in statements.EnumerateArray())
            {
                var kind = s.TryGetProperty("classified_as", out var ca) && ca.GetArrayLength() > 0 ? Str(ca[0], "_label") : null;
                var content = Str(s, "content");
                if (content is null || !EnglishOrUntagged(s)) continue;
                if (description is null && (kind is null || kind.Contains("Biography", StringComparison.OrdinalIgnoreCase) || kind == "Description")) description = content;
                else facts.Add(new Fact(kind ?? "Statement", content));
                if (facts.Count >= 6) break;
            }
        foreach (var (prop, key) in new[] { ("born", "Born"), ("died", "Died"), ("formed_by", "Formed"), ("dissolved_by", "Dissolved") })
            if (r.TryGetProperty(prop, out var ev) && ev.TryGetProperty("timespan", out var ts) && ts.TryGetProperty("identified_by", out var tn) && tn.GetArrayLength() > 0 && Str(tn[0], "content") is { } when)
                facts.Add(new Fact(key, when));
        var selfEcho = false;
        string? ours = null;
        var equivalents = new List<string>();
        if (r.TryGetProperty("equivalent", out var eq))
            foreach (var e in eq.EnumerateArray())
                if (Str(e, "id") is { } id)
                {
                    equivalents.Add(id);
                    if (scheme is not null && scheme.Owns(id)) { selfEcho = true; ours ??= id; }
                }
        foreach (var e in equivalents.Where(e => AuthorityUris.SourceOf(e) != "other" && !(scheme?.Owns(e) ?? false)).Take(8)) facts.Add(new Fact("LUX says same as", e, e));
        double? lat = null, lon = null;
        if (Str(r, "defined_by") is { } wkt && Centroid(wkt) is { } centre) { lat = centre.Lat; lon = centre.Long; }
        Picture? picture = null;
        if (r.TryGetProperty("representation", out var reps))
            foreach (var rep in reps.EnumerateArray())
                if (rep.TryGetProperty("digitally_shown_by", out var shown))
                    foreach (var d in shown.EnumerateArray())
                        if (d.TryGetProperty("access_point", out var aps))
                            foreach (var ap in aps.EnumerateArray())
                                if (Str(ap, "id") is { } img && img.Contains("wikimedia.org", StringComparison.Ordinal) && picture is null)
                                    picture = await CommonsAsync(WebUtility.UrlDecode(img.Split('/').Last()), ct);
        facts.Add(new Fact("In LUX", "the record and the works that cite it", uri.Replace("/data/", "/view/")));
        var note = selfEcho ? $"LUX's record carries our identifier {ours}: this is our earlier export reflected back, not a second witness (doc 10 7.4)." : null;
        return new Pulled(uri, "lux", now, label, description, names.Distinct().Take(8).ToList(), facts, Picture: picture, Lat: lat, Long: lon, SelfEcho: selfEcho, Note: note);
    }

    /// <summary>LUX writes names and statements in many languages; the panel keeps English and those with no language given (a library's own forms).</summary>
    private static bool EnglishOrUntagged(JsonElement node)
    {
        if (!node.TryGetProperty("language", out var langs) || langs.ValueKind != JsonValueKind.Array || langs.GetArrayLength() == 0) return true;
        foreach (var l in langs.EnumerateArray())
            if (Str(l, "_label") is { } name && (name.Equals("English", StringComparison.OrdinalIgnoreCase) || Str(l, "id")?.EndsWith("/en", StringComparison.Ordinal) == true)) return true;
        return false;
    }

    /// <summary>The centre of a WKT POINT or POLYGON (LUX's <c>defined_by</c>), as latitude and longitude; WKT writes longitude first.</summary>
    public static (double Lat, double Long)? Centroid(string wkt)
    {
        var numbers = Regex.Matches(wkt, @"-?\d+(?:\.\d+)?").Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToList();
        if (numbers.Count < 2 || numbers.Count % 2 != 0) return null;
        var lons = new List<double>(); var lats = new List<double>();
        for (var i = 0; i < numbers.Count; i += 2) { lons.Add(numbers[i]); lats.Add(numbers[i + 1]); }
        return ((lats.Min() + lats.Max()) / 2, (lons.Min() + lons.Max()) / 2);
    }

    // ---- helpers ----

    private static string? Str(JsonElement e, string prop) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static readonly Regex Tags = new("<[^>]+>", RegexOptions.Compiled);

    /// <summary>Commons' metadata values are HTML fragments (an author as a link); the text alone is kept.</summary>
    public static string? StripHtml(string? html) => html is null ? null : WebUtility.HtmlDecode(Tags.Replace(html, "")).Trim() is { Length: > 0 } s ? s : null;
}
