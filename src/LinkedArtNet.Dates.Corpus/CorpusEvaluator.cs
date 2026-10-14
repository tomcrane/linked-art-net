using System.Globalization;
using System.Text;
using LinkedArtNet.Dates.Realisation;

namespace LinkedArtNet.Dates.Corpus;

/// <summary>Outcome of one entry: pass, or the list of expectations it missed.</summary>
public sealed record EntryResult(CorpusEntry Entry, TimeSpanParseResult Result, IReadOnlyList<string> Failures)
{
    public bool Passed => Failures.Count == 0;
}

/// <summary>Runs corpus entries through a parser and checks each set field of the expectation.</summary>
public sealed class CorpusEvaluator
{
    private readonly Func<string, ParseProfile> _profiles;
    private readonly Func<ParseProfile, TimeSpanParser> _parsers;

    /// <param name="profiles">Resolves a profile name; the default resolves only "default". Pass <c>ProfileCatalog.Load(dir).Get</c> for the repository's profiles.</param>
    /// <param name="parserFactory">Builds a parser for a profile; the default is the standard chain.</param>
    public CorpusEvaluator(Func<string, ParseProfile>? profiles = null, Func<ParseProfile, TimeSpanParser>? parserFactory = null)
    {
        _profiles = profiles ?? (name => name == "default" ? ParseProfile.Default : throw new KeyNotFoundException($"Unknown profile '{name}'."));
        var cache = new Dictionary<ParseProfile, TimeSpanParser>();
        var factory = parserFactory ?? (p => new TimeSpanParser(p, currentYear: () => 2026));
        _parsers = p => cache.TryGetValue(p, out var parser) ? parser : cache[p] = factory(p);
    }

    public EntryResult Evaluate(CorpusEntry entry)
    {
        var profile = _profiles(entry.Profile);
        var result = _parsers(profile).Parse(entry.Input, entry.ParsedIntent);
        return new EntryResult(entry, result, Check(entry.Expect, result));
    }

    public IReadOnlyList<EntryResult> Evaluate(IEnumerable<CorpusEntry> entries) => entries.Select(Evaluate).ToList();

    /// <summary>The expectations a result misses; empty when it satisfies all of them.</summary>
    public static IReadOnlyList<string> Check(Expectation expect, TimeSpanParseResult result)
    {
        var failures = new List<string>();
        void Expect(bool ok, string message) { if (!ok) failures.Add(message); }

        if (expect.Status is { } status) Expect(string.Equals(result.Status.ToString(), status, StringComparison.OrdinalIgnoreCase), $"status: expected {status}, got {result.Status}");
        if (expect.Kind is { } kind) Expect(string.Equals(result.Kind.ToString(), kind, StringComparison.OrdinalIgnoreCase), $"kind: expected {kind}, got {result.Kind}");
        if (expect.Canonical is { } canonical) Expect(result.Canonical == canonical, $"canonical: expected '{canonical}', got '{result.Canonical ?? "(none)"}'");
        if (expect.Edtf is { } edtf) Expect(result.Edtf == edtf, $"edtf: expected '{edtf}', got '{result.Edtf ?? "(none)"}'");
        if (expect.TimeSpan is { } ts) CheckSpan("timespan", ts, result.TimeSpan, expect.ToleranceYears, failures);
        if (expect.Begin is { } begin) CheckSpan("begin", begin, result.Begin, expect.ToleranceYears, failures);
        if (expect.End is { } end) CheckSpan("end", end, result.End, expect.ToleranceYears, failures);
        if (expect.Years is { } years)
        {
            var span = result.TimeSpan ?? result.Begin;
            var last = result.TimeSpan ?? result.End;
            if (years.Start is { } ys) Expect(span?.BeginOfTheBegin is { } b && Math.Abs(b.Year - ys) <= expect.ToleranceYears, $"years.start: expected {ys}, got {span?.BeginOfTheBegin?.Year.ToString(CultureInfo.InvariantCulture) ?? "(none)"}");
            if (years.End is { } ye) Expect(last?.EndOfTheEnd is { } e && Math.Abs(e.Year - ye) <= expect.ToleranceYears, $"years.end: expected {ye}, got {last?.EndOfTheEnd?.Year.ToString(CultureInfo.InvariantCulture) ?? "(none)"}");
        }
        foreach (var flag in expect.Flags ?? [])
            Expect(result.Flags.Any(f => string.Equals(f.ToString(), flag, StringComparison.OrdinalIgnoreCase)), $"flag {flag} missing (flags: {string.Join(", ", result.Flags)})");
        foreach (var flag in expect.NotFlags ?? [])
            Expect(!result.Flags.Any(f => string.Equals(f.ToString(), flag, StringComparison.OrdinalIgnoreCase)), $"flag {flag} present");
        if (expect.MinConfidence is { } min) Expect(result.Confidence >= min, $"confidence {result.Confidence:0.00} below {min}");
        if (expect.MaxConfidence is { } max) Expect(result.Confidence <= max, $"confidence {result.Confidence:0.00} above {max}");
        return failures;
    }

    private static void CheckSpan(string name, ExpectedTimeSpan expected, LinkedArtTimeSpan? actual, int toleranceYears, List<string> failures)
    {
        if (actual is null) { failures.Add($"{name}: expected a span, got none"); return; }
        CheckBound($"{name}.begin_of_the_begin", expected.BeginOfTheBegin, actual.BeginOfTheBegin, toleranceYears, failures);
        CheckBound($"{name}.end_of_the_begin", expected.EndOfTheBegin, actual.EndOfTheBegin, toleranceYears, failures);
        CheckBound($"{name}.begin_of_the_end", expected.BeginOfTheEnd, actual.BeginOfTheEnd, toleranceYears, failures);
        CheckBound($"{name}.end_of_the_end", expected.EndOfTheEnd, actual.EndOfTheEnd, toleranceYears, failures);
    }

    // A bound the expectation leaves out must be absent; a set bound must match (within the tolerance, in years).
    private static void CheckBound(string name, string? expected, LinkedArtDate? actual, int toleranceYears, List<string> failures)
    {
        if (expected is null)
        {
            if (actual is not null) failures.Add($"{name}: expected none, got {actual}");
            return;
        }
        if (actual is null) { failures.Add($"{name}: expected {expected}, got none"); return; }
        if (actual.ToString() == expected) return;
        if (toleranceYears > 0 && LinkedArtDate.TryParse(expected, null, out var e) && Math.Abs(e.Year - actual.Value.Year) <= toleranceYears) return;
        failures.Add($"{name}: expected {expected}, got {actual}");
    }

    // ---- metrics -----------------------------------------------------------------------------------------------

    public sealed record Metrics(int Total, int Passed, int Failed, double Accuracy, double CalibrationError,
        IReadOnlyDictionary<string, (int Passed, int Total)> ByTag, IReadOnlyDictionary<string, (int Passed, int Total)> ByFile,
        IReadOnlyDictionary<string, int> StatusConfusion);

    public static Metrics Summarise(IReadOnlyList<EntryResult> results)
    {
        var passed = results.Count(r => r.Passed);
        var byTag = results.SelectMany(r => r.Entry.Tags.Select(t => (t, r))).GroupBy(x => x.t)
            .ToDictionary(g => g.Key, g => (g.Count(x => x.r.Passed), g.Count()), StringComparer.Ordinal);
        var byFile = results.GroupBy(r => r.Entry.File).ToDictionary(g => g.Key, g => (g.Count(r => r.Passed), g.Count()), StringComparer.Ordinal);
        var confusion = results.Where(r => r.Entry.Expect.Status is not null)
            .GroupBy(r => $"{r.Entry.Expect.Status!.ToLowerInvariant()}->{r.Result.Status.ToString().ToLowerInvariant()}")
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        return new Metrics(results.Count, passed, results.Count - passed, results.Count == 0 ? 1 : passed / (double)results.Count,
            ExpectedCalibrationError(results), byTag, byFile, confusion);
    }

    /// <summary>Mean over ten confidence bins of |mean confidence - pass rate|, weighted by bin size.</summary>
    public static double ExpectedCalibrationError(IReadOnlyList<EntryResult> results)
    {
        if (results.Count == 0) return 0;
        var bins = results.GroupBy(r => Math.Min(9, (int)(r.Result.Confidence * 10)));
        return bins.Sum(b => b.Count() / (double)results.Count * Math.Abs(b.Average(r => r.Result.Confidence) - b.Average(r => r.Passed ? 1.0 : 0.0)));
    }

    public static string Report(IReadOnlyList<EntryResult> results, IReadOnlyDictionary<string, bool>? previous = null)
    {
        var sb = new StringBuilder();
        foreach (var statusGroup in results.GroupBy(r => r.Entry.Status).OrderBy(g => g.Key))
        {
            var m = Summarise(statusGroup.ToList());
            sb.AppendLine($"## {statusGroup.Key.ToString().ToLowerInvariant()}: {m.Passed}/{m.Total} pass ({m.Accuracy:P1}), calibration error {m.CalibrationError:0.000}");
            sb.AppendLine();
            foreach (var (file, (p, t)) in m.ByFile.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                sb.AppendLine($"- {file}: {p}/{t}");
            sb.AppendLine();
            if (m.StatusConfusion.Count > 0)
                sb.AppendLine("Status (expected->actual): " + string.Join(", ", m.StatusConfusion.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key} {kv.Value}")));
            var tags = m.ByTag.Where(kv => kv.Value.Passed < kv.Value.Total).OrderBy(kv => kv.Value.Passed / (double)kv.Value.Total).Take(15).ToList();
            if (tags.Count > 0)
                sb.AppendLine("Weakest tags: " + string.Join(", ", tags.Select(kv => $"{kv.Key} {kv.Value.Passed}/{kv.Value.Total}")));
            sb.AppendLine();
        }
        if (previous is not null)
        {
            var regressions = results.Where(r => !r.Passed && previous.TryGetValue(r.Entry.Id, out var was) && was).Select(r => r.Entry.Id).ToList();
            var fixes = results.Where(r => r.Passed && previous.TryGetValue(r.Entry.Id, out var was) && !was).Select(r => r.Entry.Id).ToList();
            sb.AppendLine($"Since the last run: {fixes.Count} newly passing, {regressions.Count} newly failing" +
                          (regressions.Count > 0 ? ": " + string.Join(", ", regressions) : "."));
            sb.AppendLine();
        }
        var failures = results.Where(r => !r.Passed).ToList();
        if (failures.Count > 0)
        {
            sb.AppendLine("## Failures");
            sb.AppendLine();
            var groups = failures.GroupBy(Cause).OrderBy(g => g.Key).ToList();
            sb.AppendLine("By cause: " + string.Join(", ", groups.Select(g => $"{Label(g.Key).ToLowerInvariant()} {g.Count()}")) + ".");
            sb.AppendLine();
            foreach (var g in groups)
            {
                sb.AppendLine($"### {Label(g.Key)} ({g.Count()})");
                sb.AppendLine();
                sb.AppendLine(Explanation(g.Key));
                sb.AppendLine();
                foreach (var f in g)
                {
                    sb.AppendLine($"- `{f.Entry.Id}` ({f.Entry.Status.ToString().ToLowerInvariant()}) \"{f.Entry.Input}\" -> {f.Result.Canonical ?? f.Result.Status.ToString()}");
                    sb.AppendLine($"  - expected: {CorpusAcceptance.Describe(f.Entry.Expect)}");
                    foreach (var reason in f.Failures) sb.AppendLine($"  - {reason}");
                }
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    /// <summary>Why an entry failed, coarsely, so a reader can tell parser gaps from flag mapping and convention differences.</summary>
    public enum FailureCause { NotParsed, ParsedUnexpectedly, StatusDiffers, ReadingDiffers, BoundsDiffer, FlagsOnly, Other }

    public static FailureCause Cause(EntryResult r)
    {
        var reasons = r.Failures;
        if (reasons.Any(x => x.StartsWith("status:", StringComparison.Ordinal)))
        {
            var parsed = r.Result.Status is ParseStatus.Parsed or ParseStatus.Partial;
            var expectedParsed = r.Entry.Expect.Status is "parsed" or "partial";
            if (!parsed && expectedParsed) return FailureCause.NotParsed;
            if (parsed && !expectedParsed) return FailureCause.ParsedUnexpectedly;
            return FailureCause.StatusDiffers;
        }
        if (reasons.Any(x => x.StartsWith("canonical:", StringComparison.Ordinal) || x.StartsWith("edtf:", StringComparison.Ordinal) || x.StartsWith("kind:", StringComparison.Ordinal))) return FailureCause.ReadingDiffers;
        if (reasons.Any(x => x.StartsWith("years.", StringComparison.Ordinal) || x.StartsWith("timespan.", StringComparison.Ordinal) || x.StartsWith("begin.", StringComparison.Ordinal) || x.StartsWith("end.", StringComparison.Ordinal))) return FailureCause.BoundsDiffer;
        if (reasons.Count > 0 && reasons.All(x => x.StartsWith("flag ", StringComparison.Ordinal))) return FailureCause.FlagsOnly;
        return FailureCause.Other;
    }

    public static string Label(FailureCause cause) => cause switch
    {
        FailureCause.NotParsed => "Not parsed",
        FailureCause.ParsedUnexpectedly => "Parsed unexpectedly",
        FailureCause.StatusDiffers => "Status differs",
        FailureCause.ReadingDiffers => "Reading differs",
        FailureCause.BoundsDiffer => "Bounds differ",
        FailureCause.FlagsOnly => "Flags only",
        _ => "Other",
    };

    private static string Explanation(FailureCause cause) => cause switch
    {
        FailureCause.NotParsed => "The source has a date and the parser found none: grammar gaps, or strings the source read generously.",
        FailureCause.ParsedUnexpectedly => "The source says no date, or a feature we do not support, and the parser read something anyway: a false positive, or an expectation to update with the reading.",
        FailureCause.StatusDiffers => "Parsed one way, expected another (for example partial against parsed).",
        FailureCause.ReadingDiffers => "A different canonical form or kind for the same string: a parser bug, or a convention the profile should set.",
        FailureCause.BoundsDiffer => "The same reading realised to different datetimes: usually a band, season, circa or open-bound convention, or the source's own estimate.",
        FailureCause.FlagsOnly => "The bounds agree and only a hedge flag differs: usually how the importer mapped the source's flag.",
        _ => "Confidence or another check.",
    };
}
