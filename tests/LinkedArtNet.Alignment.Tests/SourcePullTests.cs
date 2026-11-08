using LinkedArtNet.Alignment.Authorities;
using LinkedArtNet.Alignment.Store;

namespace LinkedArtNet.Alignment.Tests;

/// <summary>
/// "From the sources, on demand" (plan section 7, the owner, 2026-11-08): the fetchers read the shapes the live services returned on
/// 2026-11-08 (John Robert Cozens in Wikidata, Wikipedia, Commons, ULAN and LoC; LUX's "King's Gallery", which carries a PMC identifier),
/// here as canned answers, and the result is cached on the record's row.
/// </summary>
[Collection("postgres")]
public class SourcePullTests(PostgresFixture pg)
{
    private sealed class Canned(Dictionary<string, string> answers) : IWebFetch
    {
        public List<string> Asked { get; } = [];
        public Task<WebAnswer> GetAsync(string url, string accept, CancellationToken ct = default)
        {
            Asked.Add(url);
            var hit = answers.FirstOrDefault(a => url.Contains(a.Key, StringComparison.Ordinal));
            return Task.FromResult(hit.Key is null ? WebAnswer.NotFound : WebAnswer.Of(hit.Value));
        }
    }

    private const string WikidataJson = """
        {"entities":{"Q613852":{"id":"Q613852","labels":{"en":{"language":"en","value":"John Robert Cozens"}},
        "descriptions":{"en":{"language":"en","value":"English painter (1752-1797)"}},
        "aliases":{"en":[{"language":"en","value":"J. R. Cozens"}]},
        "claims":{"P18":[{"mainsnak":{"datavalue":{"value":"John Robert Cozens 002.jpg","type":"string"}}}],
                  "P569":[{"mainsnak":{"datavalue":{"value":{"time":"+1752-00-00T00:00:00Z","precision":9},"type":"time"}}}],
                  "P570":[{"mainsnak":{"datavalue":{"value":{"time":"+1797-12-14T00:00:00Z","precision":11},"type":"time"}}}],
                  "P245":[{"mainsnak":{"datavalue":{"value":"500000438","type":"string"}}}],
                  "P244":[{"mainsnak":{"datavalue":{"value":"n80153730","type":"string"}}}],
                  "P625":[{"mainsnak":{"datavalue":{"value":{"latitude":51.5,"longitude":-0.12},"type":"globecoordinate"}}}]},
        "sitelinks":{"enwiki":{"site":"enwiki","title":"John Robert Cozens","url":"https://en.wikipedia.org/wiki/John_Robert_Cozens"}}}}}
        """;
    private const string WikipediaJson = """
        {"title":"John Robert Cozens","extract":"John Robert Cozens was an English painter of romantic watercolour landscapes.",
         "content_urls":{"desktop":{"page":"https://en.wikipedia.org/wiki/John_Robert_Cozens"}},
         "thumbnail":{"source":"https://upload.wikimedia.org/x/330px-John_Robert_Cozens_002.jpg"}}
        """;
    private const string CommonsJson = """
        {"query":{"pages":{"123":{"title":"File:John Robert Cozens 002.jpg","imageinfo":[{"thumburl":"https://upload.wikimedia.org/thumb/250px-John_Robert_Cozens_002.jpg",
         "descriptionurl":"https://commons.wikimedia.org/wiki/File:John_Robert_Cozens_002.jpg",
         "extmetadata":{"LicenseShortName":{"value":"Public domain"},"Artist":{"value":"<a href=\"https://en.wikipedia.org/wiki/John_Robert_Cozens\">John Robert Cozens</a>"},
                        "Credit":{"value":"The Yorck Project"},"AttributionRequired":{"value":"false"}}}]}}}}
        """;
    private const string UlanJson = """
        {"head":{"vars":["label","bio","start","end","nat","gender","note"]},"results":{"bindings":[{"label":{"value":"Cozens, John Robert"},
         "bio":{"value":"English painter, draftsman, and watercolorist, 1752-1797"},"start":{"value":"1752"},"end":{"value":"1797"},
         "nat":{"value":"British"},"gender":{"value":"male"},"note":{"value":"British artist. Comment on works: History; Landscapes"}}]}}
        """;
    private const string LocJson = """
        [{"@id":"http://id.loc.gov/authorities/names/n80153730","@type":["http://www.loc.gov/mads/rdf/v1#PersonalName","http://www.loc.gov/mads/rdf/v1#Authority"],
          "http://www.loc.gov/mads/rdf/v1#authoritativeLabel":[{"@value":"Cozens, John Robert, 1752-1797"}],
          "http://www.loc.gov/mads/rdf/v1#hasExactExternalAuthority":[{"@id":"http://viaf.org/viaf/12345"}]},
         {"@id":"_:v1","@type":["http://www.loc.gov/mads/rdf/v1#PersonalName","http://www.loc.gov/mads/rdf/v1#Variant"],
          "http://www.loc.gov/mads/rdf/v1#variantLabel":[{"@value":"Cozens, J. R. (John Robert), 1752-1797"}]},
         {"@id":"_:s1","@type":["http://www.loc.gov/mads/rdf/v1#Source"],
          "http://www.loc.gov/mads/rdf/v1#citationSource":[{"@value":"Burlington Fine Arts Club, London. Catalogue of a collection of drawings by John Robert Cozens, 1923:"}],
          "http://www.loc.gov/mads/rdf/v1#citationNote":[{"@value":"t.p. (John Robert Cozens)"}]},
         {"@id":"http://id.loc.gov/rwo/agents/n80153730","@type":["http://www.loc.gov/mads/rdf/v1#RWO"],
          "http://www.loc.gov/mads/rdf/v1#birthDate":[{"@value":"1752"}],"http://www.loc.gov/mads/rdf/v1#deathDate":[{"@value":"1797"}]}]
        """;
    private const string LuxJson = """
        {"id":"https://lux.collections.yale.edu/data/group/e5478e91","type":"Group","_label":"King's Gallery",
         "identified_by":[{"type":"Name","content":"King's Gallery"},{"type":"Name","content":"Queen's Gallery"},{"type":"Name","content":"女王美術館","language":[{"_label":"Chinese"}]}],
         "referred_to_by":[{"type":"LinguisticObject","content":"British repository, London, contemporary","classified_as":[{"_label":"Biography Statement"}]},
                           {"type":"LinguisticObject","content":"In Buckingham Palace.","classified_as":[{"_label":"Description"}],"language":[{"_label":"English"}]},
                           {"type":"LinguisticObject","content":"musée du palais de Buckingham","classified_as":[{"_label":"Description"}],"language":[{"_label":"French"}]}],
         "formed_by":{"type":"Formation","timespan":{"type":"TimeSpan","identified_by":[{"type":"Name","content":"1962-01-01"}]}},
         "equivalent":[{"id":"https://data.paul-mellon-centre.ac.uk/group/k3vhptjx"},{"id":"http://vocab.getty.edu/ulan/500230115"},{"id":"http://www.wikidata.org/entity/Q1434767"}],
         "defined_by":"POLYGON ((-71.27 42.40, -71.17 42.40, -71.17 42.50, -71.27 42.50, -71.27 42.40))"}
        """;

    private static Canned Web() => new(new Dictionary<string, string>
    {
        ["Special:EntityData/Q613852.json"] = WikidataJson, ["rest_v1/page/summary"] = WikipediaJson, ["commons.wikimedia.org"] = CommonsJson,
        ["vocab.getty.edu/sparql.json"] = UlanJson, ["id.loc.gov/authorities/names/n80153730.json"] = LocJson, ["lux.collections.yale.edu/data/group"] = LuxJson,
    });

    [PostgresFact]
    public async Task Wikidata_brings_the_description_the_summary_and_the_picture_with_its_attribution()
    {
        var web = Web();
        var pull = new SourcePull(pg.Store, web);
        var uri = "http://www.wikidata.org/entity/Q613852";
        var p = await pull.PullAsync(uri);
        Assert.Null(p.Error);
        Assert.Equal("John Robert Cozens", p.Label);
        Assert.Equal("English painter (1752-1797)", p.Description);
        Assert.Equal(["J. R. Cozens"], p.Aliases);
        Assert.Contains(p.Facts, f => f.Key == "Born" && f.Value == "1752");
        Assert.Contains(p.Facts, f => f.Key == "Died" && f.Value == "1797-12-14");
        Assert.Contains(p.Facts, f => f.Key == "ULAN" && f.Url == "http://vocab.getty.edu/ulan/500000438");
        Assert.StartsWith("John Robert Cozens was an English painter", p.Extract);
        Assert.Equal("English Wikipedia", p.ExtractFrom);
        Assert.Equal("https://en.wikipedia.org/wiki/John_Robert_Cozens", p.ExtractUrl);
        Assert.Equal(51.5, p.Lat); Assert.Equal(-0.12, p.Long);
        Assert.NotNull(p.Picture);
        Assert.Equal("John Robert Cozens 002.jpg", p.Picture.FileName);
        Assert.Equal("https://upload.wikimedia.org/thumb/250px-John_Robert_Cozens_002.jpg", p.Picture.ThumbUrl);
        Assert.Equal("John Robert Cozens", p.Picture.Author); // the HTML stripped
        Assert.Equal("Public domain", p.Picture.Licence);
        Assert.False(p.Picture.AttributionRequired);
        Assert.Equal("John Robert Cozens, Public domain", p.Picture.Attribution);
        Assert.Equal(3, web.Asked.Count); // the item, the summary, the file: one call each, nothing else
        // cached on the record's row, made for the purpose, and read back from it
        var record = await pg.Store.GetAuthorityAsync(uri);
        Assert.NotNull(record);
        Assert.Equal("wikidata", record.Source);
        Assert.Equal("John Robert Cozens", record.Label);
        var cached = SourcePull.Read(record);
        Assert.NotNull(cached);
        Assert.Equal(p.Picture.ThumbUrl, cached.Picture?.ThumbUrl);
        Assert.Equal(p.Facts.Count, cached.Facts.Count);
        // an existing row keeps what the dump gave it, and gains the pull
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(uri, "wikidata", "John Robert Cozens", ["Cozens, John Robert"], "Person", null, [], """{"ids":{"viaf":"1"}}""", DateTimeOffset.UtcNow, "dump"));
        Assert.Null(SourcePull.Read(await pg.Store.GetAuthorityAsync(uri))); // the dump reload replaced data whole
        await pull.PullAsync(uri);
        record = await pg.Store.GetAuthorityAsync(uri);
        Assert.Contains("Cozens, John Robert", record!.Labels);
        Assert.Contains("\"ids\"", record.DataJson);
        Assert.NotNull(SourcePull.Read(record));
    }

    [PostgresFact]
    public async Task Ulan_and_loc_bring_the_biography_the_dates_the_variants_and_the_sources()
    {
        var pull = new SourcePull(pg.Store, Web());
        var ulan = await pull.PullAsync("http://vocab.getty.edu/ulan/500000438");
        Assert.Null(ulan.Error);
        Assert.Equal("Cozens, John Robert", ulan.Label);
        Assert.Equal("English painter, draftsman, and watercolorist, 1752-1797", ulan.Description);
        Assert.Contains(ulan.Facts, f => f.Key == "Nationality" && f.Value == "British");
        Assert.Contains(ulan.Facts, f => f.Key == "Dates" && f.Value == "1752 to 1797");
        Assert.Contains(ulan.Facts, f => f.Key == "Full record" && f.Url!.Contains("subjectid=500000438"));

        var loc = await pull.PullAsync("http://id.loc.gov/authorities/names/n80153730");
        Assert.Null(loc.Error);
        Assert.Equal("Cozens, John Robert, 1752-1797", loc.Label);
        Assert.Equal(["Cozens, J. R. (John Robert), 1752-1797"], loc.Aliases);
        Assert.Contains(loc.Facts, f => f.Key == "Found in" && f.Value.StartsWith("Burlington Fine Arts Club") && f.Value.EndsWith("t.p. (John Robert Cozens)"));
        Assert.Contains(loc.Facts, f => f.Key == "Same as" && f.Value == "http://viaf.org/viaf/12345");
        Assert.Contains(loc.Facts, f => f.Key == "Born" && f.Value == "1752");
        Assert.Contains(loc.Facts, f => f.Key == "Died" && f.Value == "1797");
    }

    [PostgresFact]
    public async Task Lux_is_marked_a_self_echo_when_its_record_carries_our_identifier()
    {
        var ours = new IdentifierScheme("https://data.paul-mellon-centre.ac.uk/");
        var pull = new SourcePull(pg.Store, Web(), ours);
        var lux = await pull.PullAsync("https://lux.collections.yale.edu/data/group/e5478e91");
        Assert.Null(lux.Error);
        Assert.Equal("King's Gallery", lux.Label);
        Assert.Equal(["Queen's Gallery"], lux.Aliases);
        Assert.Equal("British repository, London, contemporary", lux.Description);
        Assert.Contains(lux.Facts, f => f.Key == "Description" && f.Value == "In Buckingham Palace.");
        Assert.DoesNotContain(lux.Facts, f => f.Value.Contains("musée")); // English and untagged only, from LUX's many languages
        Assert.Contains(lux.Facts, f => f.Key == "Formed" && f.Value == "1962-01-01");
        Assert.True(lux.SelfEcho);
        Assert.Contains("https://data.paul-mellon-centre.ac.uk/group/k3vhptjx", lux.Note);
        Assert.Contains(lux.Facts, f => f.Key == "LUX says same as" && f.Value == "http://vocab.getty.edu/ulan/500230115");
        Assert.DoesNotContain(lux.Facts, f => f.Value.Contains("paul-mellon-centre")); // our own id is the note, not a fact
        Assert.Equal(42.45, lux.Lat!.Value, 2); Assert.Equal(-71.22, lux.Long!.Value, 2);

        // another project's scheme: the same record is a witness like any other
        var other = await new SourcePull(pg.Store, Web(), new IdentifierScheme("https://data.example.org/x/")).PullAsync("https://lux.collections.yale.edu/data/group/e5478e91");
        Assert.False(other.SelfEcho);
        Assert.Null(other.Note);
    }

    [PostgresFact]
    public async Task A_source_that_does_not_answer_is_reported_and_not_cached()
    {
        var pull = new SourcePull(pg.Store, new Canned([]));
        var uri = "http://www.wikidata.org/entity/Q999999999";
        var p = await pull.PullAsync(uri);
        Assert.Equal("Wikidata answered 404 Not Found.", p.Error);
        var lux = await pull.PullAsync("https://lux.collections.yale.edu/data/concept/gone");
        Assert.StartsWith("LUX answered 404 Not Found. LUX has re-minted", lux.Error);
        Assert.Equal("did not answer: timed out after 20 seconds", new WebAnswer(null, 0, "timed out after 20 seconds").Said);
        Assert.Equal("answered 503 Service Unavailable", new WebAnswer(null, 503).Said);
        Assert.Null(await pg.Store.GetAuthorityAsync(uri));
        Assert.False(SourcePull.CanPull("http://viaf.org/viaf/1"));
        Assert.True(SourcePull.CanPull("https://lux.collections.yale.edu/data/place/abc"));
        var none = await pull.PullAsync("http://viaf.org/viaf/1");
        Assert.Equal("No fetcher for this source.", none.Error);
    }

    [Fact]
    public void Wikidata_times_keep_their_precision_and_commons_html_is_stripped()
    {
        Assert.Equal("John Robert Cozens", SourcePull.StripHtml("<bdi><a href=\"x\">John Robert Cozens</a></bdi>"));
        Assert.Equal("Ragesoss (assumed)", SourcePull.CleanAuthor("No machine-readable author provided. Ragesoss assumed (based on copyright claims)."));
        Assert.Equal("John Robert Cozens", SourcePull.CleanAuthor("John Robert Cozens"));
        Assert.Null(SourcePull.CleanAuthor(null));
        Assert.Null(SourcePull.StripHtml("<span></span>"));
        var c = SourcePull.Centroid("POLYGON ((-71.27 42.40, -71.17 42.40, -71.17 42.50, -71.27 42.50, -71.27 42.40))");
        Assert.NotNull(c);
        Assert.Equal(42.45, c.Value.Lat, 2); Assert.Equal(-71.22, c.Value.Long, 2);
        Assert.Equal((51.5, -0.12), SourcePull.Centroid("POINT (-0.12 51.5)"));
        Assert.Null(SourcePull.Centroid("nothing"));
    }
}
