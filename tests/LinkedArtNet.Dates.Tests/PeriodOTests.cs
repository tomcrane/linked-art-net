using LinkedArtNet.Dates.Periods;

namespace LinkedArtNet.Dates.Tests;

public class PeriodOTests
{
    private static string Fixture => Path.Combine(AppContext.BaseDirectory, "Fixtures", "periodo-mini.json");

    private static PeriodOData Mini()
    {
        using var stream = File.OpenRead(Fixture);
        return PeriodOData.FromDataset(stream, "test-1", "2026-01-01");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "linked-art-net.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root");
    }

    [Fact]
    public void Compaction_keeps_ids_labels_bounds_and_authorities()
    {
        var data = Mini();
        Assert.Equal(2, data.Authorities.Count);
        Assert.Equal(4, data.Periods.Count);
        Assert.Equal("CC0-1.0", data.Licence);
        Assert.Equal("test-1", data.Version);

        var eb2 = data.Periods.Single(p => p.Id == "p0f65r2qmh2");
        Assert.Equal("EB II", eb2.Label);
        Assert.Contains("Early Bronze II", eb2.AltLabels);
        Assert.Equal(-3099, eb2.StartEarliest);
        Assert.Equal(-2649, eb2.StopLatest);
        Assert.Equal("Levant", eb2.Spatial);
        Assert.Equal("http://n2t.net/ark:/99152/p0f65r2qmh2", eb2.Uri);

        var britain = data.Periods.Single(p => p.Id == "p0mn2ndiron"); // years given as numbers
        Assert.Equal(-799, britain.StartEarliest);
        Assert.Equal(43, britain.StopLatest);
        Assert.Contains("Âge du fer", britain.AltLabels);

        var fire = data.Periods.Single(p => p.Id == "p0mn2ndfire"); // a range for the start, an open stop
        Assert.Equal(-1_500_000, fire.StartEarliest);
        Assert.Equal(-40_000, fire.StartLatest);
        Assert.Null(fire.StopLatest);

        Assert.Equal("Life in biblical Israel", data.Authorities.Single(a => a.Id == "p0f65r2").Title);
        Assert.Equal("British Museum collection periods", data.Authorities.Single(a => a.Id == "p0mn2nd").Title);
    }

    [Fact]
    public void Compact_file_round_trips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"periodo-{Guid.NewGuid():N}.json");
        try
        {
            Mini().Save(path);
            var loaded = PeriodOData.Load(path);
            Assert.Equal(4, loaded.Periods.Count);
            Assert.Equal("test-1", loaded.Version);
            Assert.Equal(Mini().Periods.Select(p => (p.Id, p.Label, p.StartEarliest, p.StopLatest, string.Join("|", p.AltLabels))), loaded.Periods.Select(p => (p.Id, p.Label, p.StartEarliest, p.StopLatest, string.Join("|", p.AltLabels))));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void A_name_one_source_defines_resolves_to_that_definition()
    {
        var source = new PeriodOSource(Mini());
        Assert.True(source.TryResolve("Early Bronze II", out var period));
        Assert.Equal("p0f65r2qmh2", period.Key);
        Assert.Equal("EB II", period.Label);
        Assert.Equal("-3099/-2649", period.Resolved!.Canonical);
        Assert.Equal("http://n2t.net/ark:/99152/p0f65r2qmh2", period.Uri);
        Assert.Contains("Life in biblical Israel", period.Source);
        Assert.Contains("Levant", period.Source);
    }

    [Fact]
    public void A_shared_name_resolves_to_the_hull_unless_narrowed()
    {
        var source = new PeriodOSource(Mini());
        Assert.True(source.TryResolve("Iron Age", out var hull));
        Assert.Equal("periodo-iron-age", hull.Key);
        Assert.Equal("Iron Age", hull.Label);
        Assert.Equal("-1199/0043", hull.Resolved!.Canonical);
        Assert.Null(hull.Uri);
        Assert.Contains("2 definitions", hull.Source);
        Assert.Contains("1200 BCE to 43", hull.Source);

        Assert.False(source.TryResolve("iron age period", out _)); // the generic suffix is the strategy's business, so curated tables keep priority

        var british = new PeriodOSource(Mini(), new PeriodOOptions { Place = "Britain" });
        Assert.True(british.TryResolve("Iron Age", out var narrowed));
        Assert.Equal("p0mn2ndiron", narrowed.Key);
        Assert.Equal("-0799/0043", narrowed.Resolved!.Canonical);

        var french = new PeriodOSource(Mini(), new PeriodOOptions { Authorities = ["p0mn2nd"] });
        Assert.True(french.TryResolve("Âge du fer", out var alt));
        Assert.Equal("p0mn2ndiron", alt.Key);

        Assert.Equal(2, source.Definitions("Iron Age").Count);
        Assert.False(source.TryResolve("Age of Nothing", out _));
    }

    [Fact]
    public void Keys_resolve_ids_and_hulls()
    {
        var source = new PeriodOSource(Mini());
        Assert.True(source.TryResolveKey("p0mn2ndfire", out var fire));
        Assert.Equal("Y-1500000/..", fire.Resolved!.Canonical);
        Assert.True(source.TryResolveKey("periodo-iron-age", out var hull));
        Assert.Equal("-1199/0043", hull.Resolved!.Canonical);
        Assert.False(source.TryResolveKey("edo", out _));
    }

    [Fact]
    public void The_parser_reads_periodo_names_after_the_built_in_tables()
    {
        var path = Path.Combine(Path.GetTempPath(), $"periodo-{Guid.NewGuid():N}.json");
        try
        {
            Mini().Save(path);
            var parser = new TimeSpanParser(periods: PeriodOSource.WithBuiltIn(path));

            var eb2 = parser.Parse("Early Bronze II");
            Assert.Equal(ParseStatus.Parsed, eb2.Status);
            Assert.Equal("period:p0f65r2qmh2", eb2.Canonical);
            Assert.Contains(DateFlag.NamedPeriod, eb2.Flags);
            Assert.Contains(eb2.Notes, n => n.Contains("Life in biblical Israel"));

            var again = parser.Parse("period:p0f65r2qmh2"); // the canonical form resolves through the same source
            Assert.Equal(eb2.TimeSpan!.BeginOfTheBegin, again.TimeSpan!.BeginOfTheBegin);
            Assert.Equal(eb2.TimeSpan.EndOfTheEnd, again.TimeSpan.EndOfTheEnd);

            Assert.Equal("period:edo", parser.Parse("Edo period").Canonical); // built-in tables answer first
            Assert.Equal("period:edo", parser.Parse("Edo era").Canonical); // and keep priority when the strategy drops a generic suffix

            var late = parser.Parse("Late Iron Age period");
            Assert.Equal("late period:periodo-iron-age", late.Canonical);
            Assert.Contains(DateFlag.Band, late.Flags);
            Assert.Equal("period:p0f65r2qmh2~", parser.Parse("c. Early Bronze II").Canonical);

            // Names are never read inside longer text: the dates win and the nationality stays unconsumed.
            Assert.Equal("1400/1445", parser.Parse("Iron Age painter, 1400-1445").Canonical);

            Assert.Same(JsonNamedPeriodSource.BuiltIn, PeriodOSource.WithBuiltIn(Path.Combine(Path.GetTempPath(), "no-such-periodo.json")));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void The_committed_dataset_loads_and_is_credited()
    {
        var path = Path.Combine(RepoRoot(), "periods", "periodo.json");
        Assert.True(File.Exists(path), "periods/periodo.json is missing; run: dotnet run --project tools/Corpus -- periodo");
        var source = PeriodOSource.Load(path);
        Assert.True(source.Count > 9000, $"only {source.Count} periods");
        Assert.True(source.Data.Authorities.Count > 400);
        Assert.Equal("CC0-1.0", source.Data.Licence);
        Assert.Contains("perio.do", source.Data.Attribution);
        Assert.Matches(@"\d", source.Data.Version);

        Assert.True(source.TryResolve("Iron Age", out var ironAge));
        Assert.StartsWith("periodo-", ironAge.Key);
        Assert.True(source.Definitions("Iron Age").Count > 20);
        Assert.True(source.TryResolve("Early Bronze II", out _));

        var parser = new TimeSpanParser(periods: PeriodOSource.ForRepository(RepoRoot()));
        Assert.Equal("period:edo", parser.Parse("Edo period").Canonical);
        Assert.Equal(ParseStatus.Parsed, parser.Parse("Iron Age").Status);
    }
}
