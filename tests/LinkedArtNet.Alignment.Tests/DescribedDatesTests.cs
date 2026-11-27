using LinkedArtNet.Agents;
using LinkedArtNet.Alignment.Authorities;
using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

/// <summary>
/// The date and single-candidate rules of 2026-11-27, each from what the desk run of 2026-11-25 met and counted: working dates read
/// from a record's own description, the other year of a doubt its description states, an undated namesake beside a record whose dates
/// agree, a dated heading an undated author may claim when its life holds the records' dates, a heading placed by its town against a
/// string placed by its county, and a concept whose label says in brackets what kind of thing it is.
/// </summary>
[Collection("postgres")]
public class DescribedDatesTests(PostgresFixture pg)
{
    private static readonly AgentStringParser Parser = new(AgentProfileCatalog.BuiltIn.Get("default"));
    private static SourceString S(string raw, string context = "keywords") => new(1, "ds", context, raw, StringKey.Normalise(raw), null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1);
    private static AuthorityRecord Ulan(string id, string label, string? start, string? end, string description) =>
        new("http://vocab.getty.edu/ulan/" + id, "ulan", label, [label], "Person", GettyVocabLoader.LifespanFromYears(start, end, description), [], System.Text.Json.JsonSerializer.Serialize(new { description }), DateTimeOffset.UtcNow, "test");

    [Fact]
    public void Working_dates_are_read_from_a_description()
    {
        var active = Lifespan.ActiveIn("Scottish painter, active 1887-1938")!;
        Assert.True(active.IsWorking);
        Assert.Equal((1887, 1938), (active.BeginLatest!.Value.Year, active.EndEarliest!.Value.Year));
        Assert.Equal("active 1887-1938", active.Text);
        Assert.Equal((1813, 1831), (Lifespan.ActiveIn("British painter, active ca. 1813-1831")!.BeginLatest!.Value.Year, Lifespan.ActiveIn("British painter, active ca. 1813-1831")!.EndEarliest!.Value.Year));
        Assert.Equal(1864, Lifespan.ActiveIn("British artist, active 1864")!.EndEarliest!.Value.Year);
        Assert.Equal(1874, Lifespan.ActiveIn("English painter, flourished 1850-74")!.EndEarliest!.Value.Year);
        Assert.Null(Lifespan.ActiveIn("English painter, active 1780s"));
        Assert.Null(Lifespan.ActiveIn("Swedish painter, born 1656 or 1659, died 1743, active in England"));
        Assert.Null(Lifespan.ActiveIn("French printmaker, active 18th century"));
        Assert.Null(Lifespan.ActiveIn(null));
    }

    [Fact]
    public void A_catalogues_working_dates_agree_with_the_ones_a_description_states()
    {
        var rule = new ContextRule([EntityClass.Person], "default");
        // "op" is the photographic archive's "operated"; ULAN dates the painter by an estimated life and says "active" in words
        var stewart = StringReading.Read(S("Stewart, Charles Edward, op 1887-1938"), rule, Parser);
        var record = Ulan("500002420", "Stewart, Charles Edward", "1857", "1968", "Scottish painter, active 1887-1938");
        var scored = Reconciler.Score(stewart, [LocalIndexSource.ToCandidate(record, stewart, "local")], 120).Single();
        Assert.True(scored.DatesAgree);
        Assert.False(scored.Veto);
        Assert.Contains("dates agree", scored.Reason);
        // a start some years out is one working life all the same; working lives too far apart are two people
        var hastings = StringReading.Read(S("Hastings, Thomas, op 1804-1831"), rule, Parser);
        Assert.True(Reconciler.Score(hastings, [LocalIndexSource.ToCandidate(Ulan("1", "Hastings, Thomas", "1783", "1861", "British painter, active ca. 1813-1831"), hastings, "local")], 120).Single().DatesAgree);
        Assert.True(Reconciler.Score(hastings, [LocalIndexSource.ToCandidate(Ulan("2", "Hastings, Thomas", "1870", "1990", "American architect, active 1900-1929"), hastings, "local")], 120).Single().Veto);
        // one working year against the same year is agreement, not the year's last day after its first
        var bouch = StringReading.Read(S("Bouch, op 1797"), rule, Parser);
        Assert.True(Reconciler.Score(bouch, [LocalIndexSource.ToCandidate(Ulan("6", "Bouch", "1767", "1877", "British painter, active 1797"), bouch, "local")], 120).Single().DatesAgree);
        // and a birth year is no longer at odds with the estimate ULAN makes from one active year: "active 2003" was read as born by 1973
        var gillick = StringReading.Read(S("Gillick, James, 1972-"), rule, Parser);
        var estimated = Reconciler.Score(gillick, [LocalIndexSource.ToCandidate(Ulan("3", "Gillick, James", "1973", "2123", "British artist, active 2003"), gillick, "local")], 120).Single();
        Assert.False(estimated.Veto);
        Assert.False(estimated.DatesAgree); // nothing confirms him either: a birth year and a working year are not comparable
        // a record its source dates is read by those dates, whatever its description adds
        var dated = LocalIndexSource.ToCandidate(Ulan("4", "Graham, George", "1881", "1949", "English painter, 1881-1949, active 1900-1940"), gillick, "local");
        Assert.False(dated.Lifespan!.IsWorking);
    }

    [Fact]
    public void The_other_year_of_a_doubt_the_description_states_is_no_conflict()
    {
        var rule = new ContextRule([EntityClass.Person], "default");
        var roestraten = StringReading.Read(S("Roestraeten, Pieter Gerritsz van, 1630-1700"), rule, Parser);
        var record = Ulan("5", "Roestraeten, Pieter Gerritsz van", "1629", "1700", "Dutch painter, 1629 or 1630-1700, active in England");
        Assert.Equal(DateAgreement.Conflict, roestraten.Lifespan!.Compare(record.Lifespan!, 120));   // by the date fields alone
        var scored = Reconciler.Score(roestraten, [LocalIndexSource.ToCandidate(record, roestraten, "local")], 120).Single();
        Assert.False(scored.Veto);
        Assert.True(scored.DatesAgree);
        // a year the description does not state is a conflict as before
        var other = StringReading.Read(S("Roestraeten, Pieter Gerritsz van, 1640-1700"), rule, Parser);
        Assert.True(Reconciler.Score(other, [LocalIndexSource.ToCandidate(record, other, "local")], 120).Single().Veto);
    }

    [PostgresFact]
    public async Task An_undated_namesake_does_not_hold_a_string_whose_dates_one_record_agrees_with()
    {
        var ds = "dd-" + Guid.NewGuid().ToString("N")[..6];
        var tag = ds[3..];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        var options = ProjectManifest.PmcDefault.ToOptions();
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan"), new LocalIndexSource(pg.Store, "loc")], options);
        var name = $"Morgan{tag}, John";
        await pg.Store.PutAuthorityAsync(new AuthorityRecord($"http://vocab.getty.edu/ulan/{tag}1", "ulan", name, [name], "Person", GettyVocabLoader.LifespanFromYears("1823", "1886", "English painter, 1823-1886"), [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord($"http://id.loc.gov/authorities/names/{tag}2", "loc", name, [name], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord($"http://id.loc.gov/authorities/names/{tag}3", "loc", name, [name], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        var s = await pg.Store.CollectAsync(ds, "keywords", $"{name}, 1823-1886");
        var e = await reconciler.EvaluateAsync(ds, s);
        Assert.Equal(Outcome.Accepted, e.Outcome);
        Assert.Equal(DecisionMethod.Single, e.AcceptedMethod);
        Assert.Equal([$"http://vocab.getty.edu/ulan/{tag}1"], e.Accepted!.Uris);
        // with no dates of its own the string is the desk's, as before
        var undated = await pg.Store.CollectAsync(ds, "keywords", name);
        Assert.Equal(Outcome.Queued, (await reconciler.EvaluateAsync(ds, undated)).Outcome);
    }

    [PostgresFact]
    public async Task An_undated_author_claims_a_dated_heading_whose_life_holds_the_records()
    {
        var ds = "al-" + Guid.NewGuid().ToString("N")[..6];
        var tag = ds[3..];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());
        async Task<SourceString> Author(string surname, string heading, string? born, string? died, string year, string context = "persauthorfull")
        {
            var label = $"{surname}{tag}, Kathleen";
            await pg.Store.PutAuthorityAsync(new AuthorityRecord($"http://id.loc.gov/authorities/names/{tag}{surname}", "loc", $"{label}, {heading}", [label], "Person",
                GettyVocabLoader.LifespanFromYears(born, died), [], """{"headingDates":"x"}""", DateTimeOffset.UtcNow, "test"));
            var s = await pg.Store.CollectAsync(ds, context, context == "persauthorfull" ? $"{label} (author)" : label);
            await pg.Store.PutExamplesAsync([new StringExample(s.Id, surname + tag, "library", "A catalogue", year, context, "{}")]);
            return s;
        }
        // alive at the record's date: the heading is hers
        var alive = await reconciler.EvaluateAsync(ds, await Author("Fenwick", "1901-1973", "1901", "1973", "1960"));
        Assert.Equal(Outcome.Accepted, alive.Outcome);
        Assert.Equal(DecisionMethod.ExactSingle, alive.AcceptedMethod);
        Assert.Contains("alive at its record's date (1960)", alive.Accepted!.Best.Reason);
        // dead before the book (the Winter of 1848-1919 for a book of 1948), or older than a life at it: the desk's, as before
        Assert.Equal(Outcome.Queued, (await reconciler.EvaluateAsync(ds, await Author("Winter", "1848-1919", "1848", "1919", "1948"))).Outcome);
        Assert.Equal(Outcome.Queued, (await reconciler.EvaluateAsync(ds, await Author("Smith", "1868-", "1868", null, "1990"))).Outcome);
        // a living author with no death stated
        Assert.Equal(Outcome.Accepted, (await reconciler.EvaluateAsync(ds, await Author("Cross", "1936-", "1936", null, "2008"))).Outcome);
        // a heading that bears the name only as a variant is not claimed so
        var nick = $"Hardie{tag}, William";
        await pg.Store.PutAuthorityAsync(new AuthorityRecord($"http://id.loc.gov/authorities/names/{tag}Hardie", "loc", $"Hardie{tag}, Bill, 1916-1995", [nick], "Person",
            GettyVocabLoader.LifespanFromYears("1916", "1995"), [], """{"headingDates":"x"}""", DateTimeOffset.UtcNow, "test"));
        var hardie = await pg.Store.CollectAsync(ds, "persauthorfull", nick + " (author)");
        await pg.Store.PutExamplesAsync([new StringExample(hardie.Id, "Hardie" + tag, "library", "Scottish painting", "1976", "persauthorfull", "{}")]);
        Assert.Equal(Outcome.Queued, (await reconciler.EvaluateAsync(ds, hardie)).Outcome);
        // and a subject heading says nothing of when its person lived: the dated heading is not claimed by the name alone
        Assert.NotEqual(Outcome.Accepted, (await reconciler.EvaluateAsync(ds, await Author("Reaney", "1890-1968", "1890", "1968", "1950", "keywords"))).Outcome);
    }

    [PostgresFact]
    public async Task A_heading_placed_by_its_town_agrees_with_a_string_placed_by_its_county()
    {
        var ds = "tc-" + Guid.NewGuid().ToString("N")[..6];
        // letters only: a heading with digits in it is read as a dated name
        var tag = new string(ds[3..].Select(ch => char.IsDigit(ch) ? (char)('g' + (ch - '0')) : ch).ToArray());
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "tgn"), new LocalIndexSource(pg.Store, "lcsh")], ProjectManifest.PmcDefault.ToOptions());
        // a county and a town of made-up names, so the test owns them in the shared index
        string county = $"Shire{tag}", town = $"Town{tag}", castle = $"Castle {tag}";
        AuthorityRecord Place(string id, string label, string parents, string type) => new($"http://vocab.getty.edu/tgn/{tag}{id}", "tgn", label, [label], "Place", null, [],
            System.Text.Json.JsonSerializer.Serialize(new { parentString = parents, placeType = type }), DateTimeOffset.UtcNow, "test");
        // the levels are read from the place types' labels in the local AAT index
        string counties = $"http://vocab.getty.edu/aat/{tag}1", towns = $"http://vocab.getty.edu/aat/{tag}2";
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(counties, "aat", "counties", ["counties"], "Type", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(towns, "aat", "inhabited places", ["inhabited places"], "Type", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(Place("1", county, "England, United Kingdom, Europe, World", counties));
        await pg.Store.PutAuthorityAsync(Place("2", town, $"{county}, England, United Kingdom, Europe, World", towns));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord($"http://id.loc.gov/authorities/subjects/{tag}c", "lcsh", $"{castle} ({town}, England)", [$"{castle} ({town}, England)"], "Type", null, [], null, DateTimeOffset.UtcNow, "test"));
        var s = await pg.Store.CollectAsync(ds, "keywords", $"{castle} ({county})");
        var e = await reconciler.EvaluateAsync(ds, s);
        var member = e.Groups.SelectMany(g => g.Members).Single(m => m.Candidate.Source == "lcsh");
        Assert.True(member.QualifierAgrees, member.Reason);
        Assert.Equal(Outcome.Accepted, e.Outcome);

        // and a concept whose brackets name no place the gazetteer has is a kind of thing, not the placed string: LCSH's "White ladies
        // (Plants)", a variant of Snowdrops, for the priory "White Ladies (Shropshire)"
        var ladies = $"Ladies {tag}";
        await pg.Store.PutAuthorityAsync(new AuthorityRecord($"http://id.loc.gov/authorities/subjects/{tag}s", "lcsh", $"Snowdrops {tag}", [$"Snowdrops {tag}", $"{ladies} (Plants{tag})"], "Type", null, [], null, DateTimeOffset.UtcNow, "test"));
        var priory = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords", $"{ladies} ({county})"));
        var snowdrops = priory.Groups.SelectMany(g => g.Members).Single(m => m.Candidate.Source == "lcsh");
        Assert.True(snowdrops.Veto, snowdrops.Reason);
        Assert.Contains("a kind of thing", snowdrops.Reason);
    }
}
