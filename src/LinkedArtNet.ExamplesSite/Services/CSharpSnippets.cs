using System.Text;

namespace LinkedArtNet.ExamplesSite.Services;

/// <summary>One way of producing the parsed result in code, shown under "In C#" on the playground.</summary>
public sealed record CSharpSnippet(string Title, string Code, string? Note = null);

/// <summary>
/// Writes the C# that produces a playground result, in every form the libraries offer: the free text through the
/// parser, the canonical expression through the parser, the model library's factories with no parser at all, and
/// the same again from .NET <see cref="DateTime"/> values where the years fit in one.
/// </summary>
public static class CSharpSnippets
{
    public static IReadOnlyList<CSharpSnippet> For(PlaygroundResult r)
    {
        var list = new List<CSharpSnippet>();
        if (r.Status is not ("parsed" or "partial")) return list;

        var pair = r.Kind is "birthDeath" or "formationDissolution";
        var text = Text(r.Kind, r.Input);
        list.Add(new("From the text", text, r.Kind == "floruit"
            ? "Active dates are not a lifespan: on a Person, WithLifespan records them as a professional activity rather than birth and death."
            : null));

        if (r.Canonical is not null && !string.Equals(r.Canonical, r.Input, StringComparison.Ordinal))
            list.Add(new("From the canonical expression", Text(r.Kind, r.Canonical),
                "The parser reads its own canonical language, so this is the stable form to store or to generate from other data."));

        var built = pair ? BuiltPair(r) : BuiltSingle(r);
        if (built is not null)
            list.Add(new("Built directly, no parser", built, Bce(r) ? "Years here are astronomical: 1 BCE is 0, 2 BCE is -1." : null));

        var net = pair ? NetPair(r) : NetSingle(r);
        list.Add(net is not null
            ? new("From .NET dates", net, "DateTimeOffset is accepted too. Ends are inclusive in Linked Art, so the end is the last second of the period.")
            : new("From .NET dates", "// Not possible for this result: DateTime covers years 1 to 9999 only.", null));
        return list;
    }

    private static string Text(string kind, string s) => kind switch
    {
        "birthDeath" => $"person.WithLifespan({Quote(s)});",
        "formationDissolution" => $"group.WithExistence({Quote(s)});",
        "floruit" => $"person.WithLifespan({Quote(s)});\nproduction.WithTimeSpan({Quote(s)});",
        _ => $"production.WithTimeSpan({Quote(s)});",
    };

    // ---- model library only ------------------------------------------------------------------------------------

    private static string? BuiltSingle(PlaygroundResult r)
    {
        if (r.Span is null) return null;
        var shape = Shape(r.Span);
        return shape switch
        {
            ("year", var a) => $"production.WithTimeSpan({a[0]});",
            ("day", var a) => $"production.WithTimeSpan({a[0]}, {a[1]}, {a[2]});",
            _ => $"production.WithTimeSpan({Factory(r.Span, shape, Model, indent: 4)}\n    .WithLabel({Quote(r.Input)}));",
        };
    }

    private static string? BuiltPair(PlaygroundResult r)
    {
        if (r.BeginSpan is null && r.EndSpan is null) return null;
        var (subject, beginWith, endWith) = r.Kind == "birthDeath" ? ("person", "WithBorn", "WithDied") : ("group", "WithFormedBy", "WithDissolvedBy");
        var sb = new StringBuilder(subject);
        if (r.BeginSpan is { } b) sb.Append($"\n    .{beginWith}(e => e.TimeSpan = {Factory(b, Shape(b), Model, indent: 8)})");
        if (r.EndSpan is { } e2) sb.Append($"\n    .{endWith}(e => e.TimeSpan = {Factory(e2, Shape(e2), Model, indent: 8)})");
        return sb.Append(';').ToString();
    }

    // ---- .NET DateTime -----------------------------------------------------------------------------------------

    private static string? NetSingle(PlaygroundResult r)
    {
        if (r.Span is null || !FitsDateTime(r.Span)) return null;
        var sb = new StringBuilder();
        var names = Declare(sb, r.Span, "");
        sb.Append($"production.WithTimeSpan({Factory(r.Span, Shape(r.Span, forceDates: true), d => $"new LinkedArtDate({names[d]})", indent: 4)}\n    .WithLabel({Quote(r.Input)}));");
        return sb.ToString();
    }

    private static string? NetPair(PlaygroundResult r)
    {
        if (r.BeginSpan is null && r.EndSpan is null) return null;
        if (r.BeginSpan is { } b0 && !FitsDateTime(b0) || r.EndSpan is { } e0 && !FitsDateTime(e0)) return null;
        var (subject, beginWith, endWith) = r.Kind == "birthDeath" ? ("person", "WithBorn", "WithDied") : ("group", "WithFormedBy", "WithDissolvedBy");
        var sb = new StringBuilder();
        var beginNames = r.BeginSpan is { } b ? Declare(sb, b, r.Kind == "birthDeath" ? "birth" : "formation") : null;
        var endNames = r.EndSpan is { } e ? Declare(sb, e, r.Kind == "birthDeath" ? "death" : "dissolution") : null;
        sb.Append(subject);
        if (r.BeginSpan is { } b1) sb.Append($"\n    .{beginWith}(e => e.TimeSpan = {Factory(b1, Shape(b1, forceDates: true), d => $"new LinkedArtDate({beginNames![d]})", indent: 8)})");
        if (r.EndSpan is { } e1) sb.Append($"\n    .{endWith}(e => e.TimeSpan = {Factory(e1, Shape(e1, forceDates: true), d => $"new LinkedArtDate({endNames![d]})", indent: 8)})");
        return sb.Append(';').ToString();
    }

    /// <summary>Declares a DateTime variable per bound and returns the variable name for each.</summary>
    private static Dictionary<LinkedArtDate, string> Declare(StringBuilder sb, LinkedArtTimeSpan ts, string prefix)
    {
        var names = new Dictionary<LinkedArtDate, string>();
        foreach (var (date, role) in Bounds(ts))
        {
            if (names.ContainsKey(date)) continue;
            var name = prefix.Length == 0 ? role : prefix + char.ToUpperInvariant(role[0]) + role[1..];
            names[date] = name;
            sb.Append($"var {name} = new DateTime({date.Year}, {date.Month}, {date.Day}, {date.Hour}, {date.Minute}, {date.Second}, DateTimeKind.Utc);\n");
        }
        return names;
    }

    private static IEnumerable<(LinkedArtDate Date, string Role)> Bounds(LinkedArtTimeSpan ts)
    {
        var inner = ts.EndOfTheBegin is not null || ts.BeginOfTheEnd is not null;
        if (ts.BeginOfTheBegin is { } a) yield return (a, inner ? "beginOfTheBegin" : "begin");
        if (ts.EndOfTheBegin is { } b) yield return (b, "endOfTheBegin");
        if (ts.BeginOfTheEnd is { } c) yield return (c, "beginOfTheEnd");
        if (ts.EndOfTheEnd is { } d) yield return (d, inner ? "endOfTheEnd" : "end");
    }

    private static bool FitsDateTime(LinkedArtTimeSpan ts) => Bounds(ts).All(b => b.Date.Year is >= 1 and <= 9999);

    private static bool Bce(PlaygroundResult r) =>
        new[] { r.Span, r.BeginSpan, r.EndSpan }.Where(s => s is not null).SelectMany(s => Bounds(s!)).Any(b => b.Date.Year <= 0);

    // ---- shapes and factories ----------------------------------------------------------------------------------

    /// <summary>
    /// The tightest factory that reproduces the span: ("year", [y]), ("years", [a, b]), ("month", [y, m]),
    /// ("day", [y, m, d]), ("between", []) or ("init", []) for anything with inner or open bounds.
    /// </summary>
    private static (string Kind, long[] Args) Shape(LinkedArtTimeSpan ts, bool forceDates = false)
    {
        var a = ts.BeginOfTheBegin; var z = ts.EndOfTheEnd;
        if (ts.EndOfTheBegin is not null || ts.BeginOfTheEnd is not null || a is null || z is null) return ("init", []);
        if (forceDates) return ("between", []);
        var s = a.Value; var e = z.Value;
        if (s.Month == 1 && s.Day == 1 && s.Hour == 0 && s.Minute == 0 && s.Second == 0 && e.Month == 12 && e.Day == 31 && IsLastSecond(e))
            return s.Year == e.Year ? ("year", [s.Year]) : ("years", [s.Year, e.Year]);
        if (s.Year == e.Year && s.Month == e.Month && s.Day == 1 && s.Hour == 0 && s.Minute == 0 && s.Second == 0
            && e.Day == LinkedArtDate.DaysInMonth(e.Year, e.Month) && IsLastSecond(e))
            return ("month", [s.Year, s.Month]);
        if (s.Year == e.Year && s.Month == e.Month && s.Day == e.Day && s.Hour == 0 && s.Minute == 0 && s.Second == 0 && IsLastSecond(e))
            return ("day", [s.Year, s.Month, s.Day]);
        return ("between", []);
    }

    private static bool IsLastSecond(LinkedArtDate d) => d is { Hour: 23, Minute: 59, Second: 59 };

    private static string Factory(LinkedArtTimeSpan ts, (string Kind, long[] Args) shape, Func<LinkedArtDate, string> date, int indent)
    {
        var pad = new string(' ', indent);
        switch (shape.Kind)
        {
            case "year": return $"LinkedArtTimeSpan.FromYear({shape.Args[0]})";
            case "years": return $"LinkedArtTimeSpan.FromYears({shape.Args[0]}, {shape.Args[1]})";
            case "month": return $"LinkedArtTimeSpan.FromMonth({shape.Args[0]}, {shape.Args[1]})";
            case "day": return $"LinkedArtTimeSpan.FromDay({shape.Args[0]}, {shape.Args[1]}, {shape.Args[2]})";
            case "between": return $"LinkedArtTimeSpan.Between({date(ts.BeginOfTheBegin!.Value)}, {date(ts.EndOfTheEnd!.Value)})";
            default:
                var sb = new StringBuilder("new LinkedArtTimeSpan\n").Append(pad).Append('{');
                foreach (var (d, _) in Bounds(ts))
                {
                    var property = d.Equals(ts.BeginOfTheBegin) ? "BeginOfTheBegin" : d.Equals(ts.EndOfTheBegin) ? "EndOfTheBegin" : d.Equals(ts.BeginOfTheEnd) ? "BeginOfTheEnd" : "EndOfTheEnd";
                    sb.Append('\n').Append(pad).Append("    ").Append(property).Append(" = ").Append(date(d)).Append(',');
                }
                return sb.Append('\n').Append(pad).Append('}').ToString();
        }
    }

    /// <summary>The most readable LinkedArtDate expression for one bound.</summary>
    private static string Model(LinkedArtDate d)
    {
        var startOfDay = d is { Hour: 0, Minute: 0, Second: 0 };
        if (startOfDay && d.Month == 1 && d.Day == 1) return $"LinkedArtDate.StartOfYear({d.Year})";
        if (d.Month == 12 && d.Day == 31 && IsLastSecond(d)) return $"LinkedArtDate.EndOfYear({d.Year})";
        if (startOfDay && d.Day == 1) return $"LinkedArtDate.StartOfMonth({d.Year}, {d.Month})";
        if (d.Day == LinkedArtDate.DaysInMonth(d.Year, d.Month) && IsLastSecond(d)) return $"LinkedArtDate.EndOfMonth({d.Year}, {d.Month})";
        if (startOfDay) return $"new LinkedArtDate({d.Year}, {d.Month}, {d.Day})";
        if (IsLastSecond(d)) return $"new LinkedArtDate({d.Year}, {d.Month}, {d.Day}).EndOfDay()";
        return $"new LinkedArtDate({d.Year}, {d.Month}, {d.Day}, {d.Hour}, {d.Minute}, {d.Second})";
    }

    private static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
