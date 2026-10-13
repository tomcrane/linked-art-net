using LinkedArtNet.Dates.Realisation;
using LinkedArtNet.Dates.Text;

namespace LinkedArtNet.Dates.Grammar;

/// <summary>
/// Strategy 2, the workhorse: a recursive-descent grammar over <see cref="Tokenizer"/> output for the idioms of
/// catalogues, archives and authority files. It produces a <see cref="DateExpression"/>, a confidence, notes, and
/// whatever text it could not consume. See docs/03-timespan-parser.md §2.2.
/// </summary>
public sealed class GrammarStrategy(Tokenizer? tokenizer = null, Periods.INamedPeriodSource? periods = null) : IParseStrategy
{
    private readonly Tokenizer _tokenizer = tokenizer ?? Tokenizer.English;
    private readonly Periods.INamedPeriodSource? _periods = periods;

    public string Name => "grammar";

    public Candidate? TryParse(string text, ParseProfile profile)
    {
        var tokens = _tokenizer.Tokenize(text);
        if (tokens.Count == 0) return null;
        return new DateParser(tokens, text, profile, _tokenizer.Lexicon, _periods).Parse();
    }
}

/// <summary>One parse of one token list. Not reusable.</summary>
internal sealed class DateParser
{
    private readonly List<Token> _t;
    private readonly string _text;
    private readonly ParseProfile _profile;
    private readonly Lexicon _lexicon;
    private readonly Periods.INamedPeriodSource? _periods;
    private readonly List<string> _notes = [];
    private readonly HashSet<DateFlag> _flags = [];
    private double _confidence = 0.95;
    private int _p;

    public DateParser(IReadOnlyList<Token> tokens, string text, ParseProfile profile, Lexicon lexicon, Periods.INamedPeriodSource? periods = null)
    {
        _t = tokens.ToList();
        _text = text;
        _profile = profile;
        _lexicon = lexicon;
        _periods = periods;
    }

    // ---- entry -------------------------------------------------------------------------------------------------

    public Candidate? Parse()
    {
        var (main, corrections, secondaries) = SplitGroups(_t);
        _t.Clear();
        _t.AddRange(main);
        _p = 0;

        DateExpression? expression = null;
        try
        {
            // Leading prose ("Italian artist, 15th century"): try again from later positions, at a cost.
            for (var start = 0; start < Math.Min(_t.Count, 10) && expression is null; start++)
            {
                _p = start;
                var notesBefore = _notes.Count;
                expression = ParseExpression();
                if (expression is null) { _notes.RemoveRange(notesBefore, _notes.Count - notesBefore); continue; }
                if (start > 0) Penalise(0.1, $"Leading text \"{_text[_t[0].Start.._t[start].Start].Trim()}\" was ignored.");
            }
        }
        catch (ArgumentException ex)
        {
            _notes.Add("The reading was structurally impossible: " + ex.Message);
            return null;
        }

        // "Not dated (1780's)": the main text has no date but a parenthetical does.
        if (expression is null or Undated && secondaries.Count > 0)
        {
            var fromGroup = ParseGroup(secondaries[0]);
            if (fromGroup is not null)
            {
                expression = fromGroup;
                secondaries.RemoveAt(0);
                Penalise(0.15, "The date was taken from a parenthetical remark.");
            }
        }
        if (expression is null) return null;

        foreach (var group in corrections)
        {
            var corrected = ParseGroup(group);
            if (corrected is null) continue;
            _notes.Add($"'i.e.' introduces a correction: {corrected.Canonical} replaces {expression.Canonical}.");
            expression = new Corrected(expression, corrected);
        }
        foreach (var group in secondaries)
        {
            var secondary = ParseGroup(group);
            if (secondary is null) continue;
            if (secondary == expression || IsRestatement(expression, secondary)) { _notes.Add("The same date is given twice."); continue; }
            _flags.Add(DateFlag.SecondaryDate);
            Penalise(0.1, $"A second date in parentheses, {secondary.Canonical}, widens the span.");
            expression = Hull(expression, secondary);
        }

        SkipNoise();
        if (_p < _t.Count)
        {
            var unconsumed = _text[_t[_p].Start..].Trim();
            Penalise(0.15, $"Could not read \"{unconsumed}\".");
            return new Candidate(expression, _confidence, "grammar", unconsumed, _notes, _flags);
        }
        return new Candidate(expression, _confidence, "grammar", null, _notes, _flags);
    }

    private DateExpression? ParseGroup(List<Token> group)
    {
        var inner = new DateParser(group, _text, _profile, _lexicon, _periods);
        var candidate = inner.Parse();
        if (candidate is null) return null;
        _notes.AddRange(inner._notes);
        foreach (var f in inner._flags) _flags.Add(f);
        return candidate.Expression;
    }

    // Parenthesised groups: "(?)" is an uncertainty mark; a group with no digits is prose ("(first edition)");
    // one starting with "i.e." is a correction; any other dated group is a secondary date. Square-bracket groups
    // inside the text are inferred dates and read inline ("[1500] - [1520]", "MDCCLXXV [1775]").
    private (List<Token> Main, List<List<Token>> Corrections, List<List<Token>> Secondaries) SplitGroups(List<Token> tokens)
    {
        var main = new List<Token>();
        var corrections = new List<List<Token>>();
        var secondaries = new List<List<Token>>();
        var i = 0;
        while (i < tokens.Count)
        {
            var t = tokens[i];
            if (t.Kind is TokenKind.OpenBracket or TokenKind.CloseBracket) { i++; continue; }
            if (t.Kind != TokenKind.OpenParen) { main.Add(t); i++; continue; }

            var depth = 0; var j = i;
            for (; j < tokens.Count; j++)
            {
                if (tokens[j].Kind == TokenKind.OpenParen) depth++;
                else if (tokens[j].Kind == TokenKind.CloseParen && --depth == 0) break;
            }
            var inner = tokens.Skip(i + 1).Take(Math.Max(0, j - i - 1)).Where(x => x.Kind is not (TokenKind.OpenParen or TokenKind.CloseParen)).ToList();
            i = j + 1;
            if (inner.Count == 1 && inner[0].Kind == TokenKind.Question) { main.Add(inner[0]); continue; }
            if (inner.Count > 0 && inner.All(x => x.Kind is TokenKind.Hedge or TokenKind.Noise or TokenKind.Floruit or TokenKind.Other) && inner.Any(x => x.Kind == TokenKind.Hedge))
            {
                main.Add(inner.First(x => x.Kind == TokenKind.Hedge));
                continue;
            }
            if (!inner.Any(x => x.Kind is TokenKind.Number or TokenKind.Ordinal or TokenKind.Roman or TokenKind.Unspecified))
            {
                _notes.Add("A parenthetical remark with no date in it was ignored.");
                continue;
            }
            if (inner[0].Kind == TokenKind.Correction) corrections.Add(inner.Skip(1).ToList());
            else secondaries.Add(inner.Where(x => x.Kind != TokenKind.Correction).ToList());
        }
        return (main, corrections, secondaries);
    }

    // ---- expression := undated | lifecycle-led | set | range | single [repeat] ------------------------------------

    private DateExpression? ParseExpression()
    {
        SkipNoise();
        if (_p >= _t.Count) return null;
        if (_t.Skip(_p).All(t => t.Kind is TokenKind.Undated or TokenKind.Noise or TokenKind.Comma or TokenKind.Question) && _t.Skip(_p).Any(t => t.Kind == TokenKind.Undated))
        {
            _p = _t.Count;
            return Undated.Instance;
        }

        var expression = ParseLifecycleLed() ?? ParseSetOrRange();
        if (expression is null) return null;

        // "1898 (...) c.1900 (...)", "[1988], ©1988": more dates follow with no connector.
        while (true)
        {
            var save = _p;
            SkipNoise();
            if (_p >= _t.Count) break;
            var next = ParseSetOrRange();
            if (next is null) { _p = save; break; }
            if (next == expression || IsRestatement(expression, next)) { _notes.Add("The same date is given twice."); continue; }
            if (IsPeriod(expression) && !IsPeriod(next)) { _notes.Add($"The explicit dates after the period name are taken: {next.Canonical}."); expression = next; continue; }
            Penalise(0.25, $"Two dates with nothing joining them, {expression.Canonical} and {next.Canonical}; the span covers both.");
            expression = Hull(expression, next);
        }
        return expression;
    }

    // "b. 1850", "fl. 1480-1510", "* 1850 † 1920", "active 1920s"
    private DateExpression? ParseLifecycleLed()
    {
        if (!TryLifecycleMarker(out var marker)) return null;
        var save = _p;
        _p++;
        SkipJoiner();
        var inner = ParseSetOrRange();
        if (inner is null) { _p = save; return null; }
        if (inner is OneOf) { _p = save; return null; }

        if (inner is not Interval && TryLifecycleMarker(out var second))
        {
            _p++;
            SkipJoiner();
            var secondInner = ParseSingleComplete();
            if (secondInner is not null)
            {
                _notes.Add("Two lifecycle markers give an explicit pair.");
                return new Interval(new Lifecycle(marker, inner), new Lifecycle(second, secondInner));
            }
        }
        if (inner is Interval { HasMarkedBounds: true }) return inner;
        return new Lifecycle(marker, inner);
    }

    private bool TryLifecycleMarker(out LifecycleMarker marker)
    {
        marker = default;
        if (_p >= _t.Count) return false;
        if (_t[_p].Kind == TokenKind.Floruit) { marker = LifecycleMarker.Floruit; return true; }
        if (_t[_p].Kind == TokenKind.Lifecycle) { marker = (LifecycleMarker)_t[_p].Value!; return true; }
        return false;
    }

    // set := range (OR range)+      "18th or 19th century": earlier members borrow what the last one has.
    private DateExpression? ParseSetOrRange()
    {
        var first = ParseRangeRaw();
        if (first is null) return null;
        var members = new List<object> { first };
        while (_p < _t.Count && _t[_p].Kind == TokenKind.Or)
        {
            var save = _p;
            _p++;
            var next = ParseRangeRaw();
            if (next is null) { _p = save; break; }
            members.Add(next);
        }
        if (members.Count == 1) return first as DateExpression ?? Finish((Piece)first);

        var last = members[^1] as Piece;
        var completed = new List<DateExpression>();
        foreach (var m in members)
        {
            var e = m switch
            {
                DateExpression d => d,
                Piece { Core: PendingOrdinal po } p when last?.Core is Century c => Finish(p with { Core = new Century((int)po.Number, c.Era) }),
                Piece { Core: PendingOrdinal po } p when last?.Core is Banded { Inner: Century c } => Finish(p with { Core = new Century((int)po.Number, c.Era) }),
                Piece { Core: PendingMonth pm } p when last?.Core is MonthPoint mp => Finish(p with { Core = new MonthPoint(mp.Year, pm.Month) }),
                Piece { Core: null, Band: not null } p when last?.Core is not null => Finish(p with { Core = last.Core is Banded bb ? bb.Inner : last.Core }),
                Piece p => Finish(p),
                _ => null,
            };
            if (e is null) return null;
            completed.Add(e);
        }
        _notes.Add("The text offers alternatives.");
        return new OneOf(completed);
    }

    /// <summary>A range as an expression, or a single as a still-completable <see cref="Piece"/>.</summary>
    private object? ParseRangeRaw()
    {
        var save = _p;
        var range = ParseRange(out var single);
        if (range is not null) return range;
        if (single is not null) return single;
        _p = save;
        return null;
    }

    private DateExpression? ParseRange() => ParseRange(out var single) ?? (single is null ? null : Finish(single));

    // range := BETWEEN single AND single | DASH single | single (DASH|TO|SLASH) (single | OPEN | end) | single OPEN
    private DateExpression? ParseRange(out Piece? single)
    {
        single = null;
        SkipNoise();
        if (_p >= _t.Count) return null;

        if (_t[_p].Kind == TokenKind.Between)
        {
            var save = _p;
            _p++;
            var a = ParseSingle();
            SkipNoise();
            if (a is not null && _p < _t.Count && _t[_p].Kind == TokenKind.RangeWord)
            {
                _p++;
                var b = ParseSingle();
                if (b is not null) return MakeInterval(a, b);
            }
            if (a is not null && Finish(a) is { } fromOnly)
            {
                _notes.Add("'from' with no end date is read as not-before.");
                return new Relative(Relation.NotBefore, fromOnly);
            }
            _p = save;
        }

        if (_t[_p].Kind == TokenKind.Dash)
        {
            var save = _p;
            _p++;
            var b = ParseSingle();
            if (b is not null && Finish(b) is { } end)
            {
                _notes.Add("A leading dash means the start is unknown.");
                return new Interval(null, end);
            }
            _p = save;
        }

        var start = ParseSingle();
        if (start is null) return null;

        if (_p < _t.Count && _t[_p].Kind == TokenKind.OpenEnd)
        {
            _p++;
            var s = Finish(start);
            return s is null ? null : new Interval(s, null);
        }
        if (_p < _t.Count && _t[_p].Kind is TokenKind.Dash or TokenKind.Slash or TokenKind.RangeWord)
        {
            var save = _p;
            var connector = _t[_p];
            _p++;
            SkipNoise();
            if (_p >= _t.Count)
            {
                if (connector.Kind == TokenKind.Dash && Finish(start) is { } open) { _notes.Add("A trailing dash means the end is open."); return new Interval(open, null); }
                _p = save;
            }
            else if (_t[_p].Kind == TokenKind.OpenEnd)
            {
                _p++;
                var s = Finish(start);
                return s is null ? null : new Interval(s, null);
            }
            else
            {
                var end = ParseSingle(abbreviationContext: start);
                // "July 4-10, 1776": a year after the range belongs to both bounds.
                if (end is not null && (NeedsYear(start.Core) || NeedsYear(end.Core)))
                {
                    var save2 = _p;
                    if (_p < _t.Count && _t[_p].Kind == TokenKind.Comma) _p++;
                    if (_p < _t.Count && IsYearShaped(_t[_p]) && _p + 1 == _t.Count)
                    {
                        var year = _t[_p].Number; _p++;
                        start = WithYear(start, year); end = WithYear(end, year);
                    }
                    else _p = save2;
                }
                if (end is not null && MakeInterval(start, end) is { } interval) return interval;
                _p = save;
            }
        }
        single = start;
        return null;
    }

    private DateExpression? MakeInterval(Piece a, Piece b)
    {
        // Completion between the bounds: shared era, shared century word, shared band target, abbreviated end.
        if (a.Era is null && b.Era is not null && a.Core is YearPoint ya && !a.EraExplicit) a = a with { Core = new YearPoint(ApplyEra(ya.Year, b.Era.Value)), Era = b.Era };
        if (a.Core is PendingOrdinal po && b.Core is Century bc) a = a with { Core = new Century((int)po.Number, bc.Era) };
        if (a.Core is PendingOrdinal po2 && b.Core is Banded { Inner: Century bc2 }) a = a with { Core = new Century((int)po2.Number, bc2.Era) };
        if (a.Core is null && a.Band is not null && b.Core is not null) a = a with { Core = b.Core is Banded bb ? bb.Inner : b.Core };
        if (a.Core is PendingMonth pm && b.Core is MonthPoint mp) a = a with { Core = new MonthPoint(mp.Year, pm.Month) };
        if (a.Core is PendingMonth pm2 && b.Core is DayPoint dp) a = a with { Core = new MonthPoint(dp.Year, pm2.Month) };
        if (a.Core is UnspecifiedDigits ua && a.AmbiguousDecade is { } ad && b.Core is Decade) a = a with { Core = new Decade(ad) };
        if (b.Core is UnspecifiedDigits ub && b.AmbiguousDecade is { } bd && a.Core is Decade) b = b with { Core = new Decade(bd) };
        // Days and months that borrow the rest of the date from the other bound: "4-10 July 1776", "1776 July 4-10",
        // "May 4 - July 10 1776", "1776 May 4 - July 10", "1776 July-September".
        if (a.Core is PendingDay pdA && b.Core is DayPoint dB) a = a with { Core = new DayPoint(dB.Year, dB.Month, pdA.Day) };
        if (a.Core is PendingDayMonth pdmA && b.Core is DayPoint dB2) a = a with { Core = new DayPoint(dB2.Year, pdmA.Month, pdmA.Day) };
        if (a.Core is PendingDayMonth pdmA2 && b.Core is MonthPoint mB) a = a with { Core = new DayPoint(mB.Year, pdmA2.Month, pdmA2.Day) };
        if (b.Core is PendingDay pdB && a.Core is DayPoint dA) b = b with { Core = new DayPoint(dA.Year, dA.Month, pdB.Day) };
        if (b.Core is PendingDayMonth pdmB && a.Core is DayPoint dA2) b = b with { Core = new DayPoint(dA2.Year, pdmB.Month, pdmB.Day) };
        if (b.Core is PendingDayMonth pdmB2 && a.Core is MonthPoint mA) b = b with { Core = new DayPoint(mA.Year, pdmB2.Month, pdmB2.Day) };
        if (b.Core is PendingMonth pmB && a.Core is MonthPoint mA2) b = b with { Core = new MonthPoint(mA2.Year, pmB.Month) };
        if (b.Core is PendingMonth pmB2 && a.Core is DayPoint dA3) b = b with { Core = new MonthPoint(dA3.Year, pmB2.Month) };

        var sa = Finish(a);
        var sb = Finish(b);
        if (sa is null || sb is null) return null;
        // "ca. 1440-after 1470": the named date is the bound and the end is open beyond it.
        if (sb is Relative { Relation: Relation.After or Relation.NotBefore } rb) { sb = rb.Inner; _flags.Add(DateFlag.OpenEnd); _notes.Add("The end is 'after' a date; that date is used as the bound."); }
        if (sa is Relative { Relation: Relation.Before or Relation.NotAfter } ra) { sa = ra.Inner; _flags.Add(DateFlag.OpenStart); _notes.Add("The start is 'before' a date; that date is used as the bound."); }
        if (sa is Relative || sb is Relative) return null;
        if (Order(sa) > Order(sb) && Order(sb) != long.MinValue) Penalise(0.2, "The bounds are in the wrong order.");
        return new Interval(sa, sb);
    }

    // ---- single := prefixes core suffixes --------------------------------------------------------------------------

    /// <summary>A partially parsed single date: modifiers plus a core that may still need completion from a neighbour.</summary>
    private sealed record Piece
    {
        public DateExpression? Core { get; init; }
        public Qualification? Hedge { get; init; }
        public Band? Band { get; init; }
        public Relation? Relation { get; init; }
        public Era? Era { get; init; }
        public bool EraExplicit { get; init; }
        public long? AmbiguousDecade { get; init; }
    }

    /// <summary>An ordinal with no century word yet ("late 13th - early 14th century").</summary>
    private sealed record PendingOrdinal(long Number) : DatePoint;

    /// <summary>A month with no year yet ("Apr-May 1903").</summary>
    private sealed record PendingMonth(int Month) : DatePoint;

    /// <summary>A day with no month or year yet ("4-10 July 1776").</summary>
    private sealed record PendingDay(int Day) : DatePoint;

    /// <summary>A day and month with no year yet ("May 4 - July 10 1776").</summary>
    private sealed record PendingDayMonth(int Day, int Month) : DatePoint;

    private static bool NeedsYear(DateExpression? core) => core is PendingMonth or PendingDay or PendingDayMonth;

    private static Piece WithYear(Piece piece, long year) => piece.Core switch
    {
        PendingMonth pm => piece with { Core = new MonthPoint(year, pm.Month) },
        PendingDayMonth pdm => piece with { Core = new DayPoint(year, pdm.Month, pdm.Day) },
        _ => piece,
    };

    private DateExpression? ParseSingleComplete()
    {
        var piece = ParseSingle();
        return piece is null ? null : Finish(piece);
    }

    private DateExpression? Finish(Piece piece)
    {
        var core = piece.Core;
        if (core is null or PendingOrdinal or PendingMonth or PendingDay or PendingDayMonth) return null;
        DateExpression e = core;
        if (piece.Band is { } band)
        {
            if (core is not (Decade or Century or Millennium or UnspecifiedDigits or YearPoint or MonthPoint or NamedPeriod)) return null;
            e = new Banded(e, band);
        }
        if (piece.Hedge is { } q) e = new Qualified(e, q);
        if (piece.Relation is { } r) e = new Relative(r, e);
        return e;
    }

    private Piece? ParseSingle(Piece? abbreviationContext = null)
    {
        var save = _p;
        var piece = new Piece();
        SkipNoise();

        // prefixes: hedge, band, relation, era-first; a dash right after a word is a joiner ("mid-1950s", "post-1945")
        Era? eraFirst = null;
        while (_p < _t.Count)
        {
            var t = _t[_p];
            if (t.Kind == TokenKind.Hedge && !IsCenturyLetterAt(_p))
            {
                piece = piece with { Hedge = Merge(piece.Hedge, (Qualification)t.Value!) };
                _p++; SkipJoiner(); continue;
            }
            if (t.Kind == TokenKind.Band && piece.Band is null) { piece = piece with { Band = (Band)t.Value! }; _p++; SkipJoiner(); SkipNoise(); continue; }
            if (t.Kind == TokenKind.Relation) { piece = piece with { Relation = (Relation)t.Value! }; _p++; SkipJoiner(); continue; }
            if (t.Kind == TokenKind.Era && eraFirst is null) { eraFirst = (Era)t.Value!; _p++; continue; }
            break;
        }

        var core = ParseCore(piece, eraFirst, abbreviationContext);
        if (core is null)
        {
            if (piece.Band is not null && piece.Hedge is null && piece.Relation is null) return piece; // "early - mid 13th century"
            _p = save;
            return null;
        }
        piece = core;

        // suffixes
        while (_p < _t.Count)
        {
            var t = _t[_p];
            if (t.Kind == TokenKind.Question) { piece = piece with { Hedge = Merge(piece.Hedge, Qualification.Uncertain) }; _p++; continue; }
            // postfix band words: "saec. XIV ex.", "s. xiv med."
            if (t.Kind == TokenKind.Band && piece.Band is null && piece.Core is Century or Decade or Millennium) { piece = piece with { Band = (Band)t.Value! }; _p++; continue; }
            if (t.Kind == TokenKind.Hedge && (!IsCenturyLetter(t) || _p + 1 >= _t.Count)) { piece = piece with { Hedge = Merge(piece.Hedge, (Qualification)t.Value!) }; _p++; continue; }
            break;
        }
        return piece;
    }

    private Piece? ParseCore(Piece piece, Era? eraFirst, Piece? context)
    {
        if (_p >= _t.Count) return null;
        var t = _t[_p];

        // deep time: 4.5 billion years ago | 10,000 BP | 10 ka BP
        if (t.Kind == TokenKind.Number && TryDeepTime(out var deep)) return piece with { Core = deep };

        // season year
        if (t.Kind == TokenKind.Season && Peek(1) is { Kind: TokenKind.Number } sy && IsYearShaped(sy))
        {
            _p += 2;
            return piece with { Core = new SeasonPoint(YearOf(sy, eraFirst, out _), (Season)t.Value!) };
        }

        // months: 8 May 1944 | May 8, 1944 | 1902 October 27 | July 2010 | Apr (pending year)
        if (t.Kind == TokenKind.Number && t.Digits <= 2 && Peek(1) is { Kind: TokenKind.Month } m1 && Peek(2) is { Kind: TokenKind.Number } y1 && IsYearShaped(y1))
        {
            _p += 3;
            return piece with { Core = Day(YearOf(y1, eraFirst, out _), (int)m1.Value!, (int)t.Number) };
        }
        // "4 May" with the year elsewhere in the range
        if (t.Kind == TokenKind.Number && t.Digits <= 2 && t.Number is >= 1 and <= 31 && Peek(1) is { Kind: TokenKind.Month } m1b && Peek(2) is not { Kind: TokenKind.Number })
        {
            _p += 2;
            return piece with { Core = new PendingDayMonth((int)t.Number, (int)m1b.Value!) };
        }
        if (t.Kind == TokenKind.Month)
        {
            // "July 4" with the year elsewhere in the range ("July 4-10, 1776", "May 4 - July 10 1776")
            if (Peek(1) is { Kind: TokenKind.Number } dOnly && dOnly.Digits <= 2 && dOnly.Number is >= 1 and <= 31 && Peek(2) is not { Kind: TokenKind.Number } && !(Peek(2) is { Kind: TokenKind.Comma } && Peek(3) is { Kind: TokenKind.Number } yc && IsYearShaped(yc)))
            {
                _p += 2;
                return piece with { Core = new PendingDayMonth((int)dOnly.Number, (int)t.Value!) };
            }
            if (Peek(1) is { Kind: TokenKind.Number } d2 && d2.Digits <= 2 && (Peek(2) is { Kind: TokenKind.Comma } ? Peek(3) : Peek(2)) is { Kind: TokenKind.Number } y2 && IsYearShaped(y2))
            {
                _p += Peek(2) is { Kind: TokenKind.Comma } ? 4 : 3;
                return piece with { Core = Day(YearOf(y2, eraFirst, out _), (int)t.Value!, (int)d2.Number) };
            }
            if (Peek(1) is { Kind: TokenKind.Number } y3 && IsYearShaped(y3))
            {
                _p += 2;
                return piece with { Core = new MonthPoint(YearOf(y3, eraFirst, out _), (int)t.Value!) };
            }
            _p++;
            return piece with { Core = new PendingMonth((int)t.Value!) };
        }
        if (t.Kind == TokenKind.Number && IsYearShaped(t) && Peek(1) is { Kind: TokenKind.Month } m3 && Peek(2) is { Kind: TokenKind.Number } d3 && d3.Digits <= 2)
        {
            _p += 3;
            return piece with { Core = Day(YearOf(t, eraFirst, out _), (int)m3.Value!, (int)d3.Number) };
        }
        // year-first archival forms: "1776 December", "1776 4 July"
        if (t.Kind == TokenKind.Number && IsYearShaped(t) && Peek(1) is { Kind: TokenKind.Month } m4)
        {
            _p += 2;
            return piece with { Core = new MonthPoint(YearOf(t, eraFirst, out _), (int)m4.Value!) };
        }
        if (t.Kind == TokenKind.Number && IsYearShaped(t) && Peek(1) is { Kind: TokenKind.Number } d5 && d5.Digits <= 2 && d5.Number is >= 1 and <= 31 && Peek(2) is { Kind: TokenKind.Month } m5)
        {
            _p += 3;
            return piece with { Core = Day(YearOf(t, eraFirst, out _), (int)m5.Value!, (int)d5.Number) };
        }

        // numeric date 13/11/2008 (day-month order from the profile)
        if (t.Kind == TokenKind.Number && t.Digits <= 2 && Peek(1) is { Kind: TokenKind.Slash } && Peek(2) is { Kind: TokenKind.Number } n2 && n2.Digits <= 2 && Peek(3) is { Kind: TokenKind.Slash } && Peek(4) is { Kind: TokenKind.Number } n3)
        {
            if (!TryWindowYear(n3, out var year)) return null;
            var (day, month) = _profile.DayMonthOrder == DayMonthOrder.DayFirst ? ((int)t.Number, (int)n2.Number) : ((int)n2.Number, (int)t.Number);
            if (day > 12 && month <= 12 && _profile.DayMonthOrder == DayMonthOrder.MonthFirst) (day, month) = (month, day);
            if (month > 12 && day <= 12) (day, month) = (month, day);
            if (t.Number <= 12 && n2.Number <= 12 && t.Number != n2.Number) { _flags.Add(DateFlag.AmbiguousDayMonth); Penalise(0.3, "Day and month could be either way round; the profile's order was used."); }
            _p += 5;
            return piece with { Core = Day(year, month, day) };
        }

        // centuries and millennia
        if (TryCentury(piece, eraFirst, out var centuryPiece)) return centuryPiece;

        // decades: 1950s, 1950's, the 2000s; abbreviated "1900-10s"
        if (t.Kind == TokenKind.Number && Peek(1) is { Kind: TokenKind.DecadeSuffix })
        {
            long value = t.Number;
            if (t.Digits <= 2 && context?.Core is YearPoint or Decade or UnspecifiedDigits && context is not null)
            {
                value = Expand(context, value);
                piece = piece with { Hedge = piece.Hedge ?? context.Hedge };
                Penalise(0.03, $"'{t.Text}s' was expanded to the {value}s from the start of the range.");
            }
            else if (t.Digits < 3) return null;
            if (value % 10 != 0) return null;
            _p += 2;
            if (value % 100 == 0)
            {
                _notes.Add($"'{t.Text}s' is read as the century {value}-{value + 99}; the decade {value}-{value + 9} is the other reading.");
                return piece with { Core = new UnspecifiedDigits($"{value / 100}XX"), AmbiguousDecade = value };
            }
            return piece with { Core = new Decade(value) };
        }

        // unspecified digits: 17_, 18--, 19XX, 19??
        if (t.Kind == TokenKind.Unspecified)
        {
            _p++;
            var pattern = (string)t.Value!;
            if (pattern.Length < 4) pattern = pattern.PadRight(4, 'X');
            var digits = pattern.TrimEnd('X');
            return piece with { Core = pattern.Length - digits.Length == 1 ? new Decade(long.Parse(pattern.Replace('X', '0'))) : new UnspecifiedDigits(pattern) };
        }

        // roman numeral year: MDCCLXXV
        if (t.Kind == TokenKind.Roman && (t.Text.Length >= 4 || t.Number >= 1000))
        {
            _p++;
            _flags.Add(DateFlag.RomanNumeral);
            _notes.Add($"{t.Text} is the roman numeral for {t.Number}.");
            return piece with { Core = new YearPoint(t.Number) };
        }

        // ordinal with no century word: completed from the other bound ("late 13th - early 14th century", "18th or 19th century")
        if (t.Kind == TokenKind.Ordinal && t.Number is >= 1 and <= 30)
        {
            _p++;
            return piece with { Core = new PendingOrdinal(t.Number) };
        }

        // a period name, with an optional regnal year: "Edo period", "Meiji 27", "Kangxi"
        if (_periods is not null && t.Kind is TokenKind.Word or TokenKind.Noise or TokenKind.Relation or TokenKind.Band or TokenKind.Roman or TokenKind.Lifecycle or TokenKind.Month or TokenKind.Season)
        {
            for (var len = Math.Min(6, _t.Count - _p); len >= 1; len--)
            {
                var run = _t.Skip(_p).Take(len).ToList();
                if (run.Any(x => x.Kind is TokenKind.Dash or TokenKind.Slash or TokenKind.Comma or TokenKind.Question or TokenKind.Or or TokenKind.RangeWord or TokenKind.Between or TokenKind.OpenEnd)) continue;
                if (run[^1].Kind == TokenKind.Number && run[^1].Digits > 3) continue;
                if (run.Count(x => x.Kind == TokenKind.Number) > 1) continue;
                var phrase = _text[run[0].Start..run[^1].End];
                if (!_periods.TryResolve(phrase, out var period)) continue;
                _p += len;
                _flags.Add(DateFlag.NamedPeriod);
                _notes.Add($"'{phrase}' is the period {period.Label} ({period.Resolved!.Canonical}).");
                Penalise(0.1, null);
                return piece with { Core = period };
            }
        }

        // a year, with its era before or after
        if (t.Kind == TokenKind.Number && !t.HasDecimal)
        {
            if (IsYearShaped(t) || eraFirst is not null || Peek(1) is { Kind: TokenKind.Era })
            {
                _p++;
                var year = YearOf(t, eraFirst, out var era);
                return piece with { Core = new YearPoint(year), Era = era, EraExplicit = era is not null };
            }
            if (t.Digits <= 2 && context?.Core is YearPoint && context is not null)
            {
                _p++;
                var year = Expand(context, t.Number);
                Penalise(0.03, $"'{t.Text}' was expanded to {year} from the start of the range.");
                return piece with { Core = new YearPoint(year), Hedge = piece.Hedge ?? context.Hedge, Era = context.Era };
            }
            if (t.Digits <= 2 && TryWindowYear(t, out var windowed))
            {
                _p++;
                return piece with { Core = new YearPoint(windowed) };
            }
            // a bare day, completed from the other bound ("4-10 July 1776", "1776 July 4-10")
            if (t.Digits <= 2 && t.Number is >= 1 and <= 31 && piece.Band is null && piece.Relation is null)
            {
                _p++;
                return piece with { Core = new PendingDay((int)t.Number) };
            }
            return null;
        }
        return null;
    }

    private bool TryCentury(Piece piece, Era? eraFirst, out Piece? result)
    {
        result = null;
        var t = _t[_p];
        // 19th century | 19th c. | XIXth century | nineteenth century | 2nd millennium BC
        if (t.Kind == TokenKind.Ordinal && Peek(1) is { } w && (w.Kind is TokenKind.CenturyWord or TokenKind.MillenniumWord || IsCenturyLetter(w)))
        {
            var millennium = w.Kind == TokenKind.MillenniumWord;
            _p += 2;
            var era = eraFirst ?? TakeEra() ?? Era.Ce;
            result = piece with { Core = millennium ? new Millennium((int)t.Number, era) : new Century((int)t.Number, era), Era = era, EraExplicit = eraFirst is not null };
            return true;
        }
        // cardinal centuries: "18c", "20 century", "17th- 18 century"
        if (t.Kind == TokenKind.Number && t.Digits <= 2 && !t.HasDecimal && t.Number is >= 1 and <= 30 && Peek(1) is { } cw
            && (cw.Kind == TokenKind.CenturyWord || (IsCenturyLetter(cw) && (Peek(2) is null || Peek(2)!.Kind is not TokenKind.Number))))
        {
            _p += 2;
            var era = eraFirst ?? TakeEra() ?? Era.Ce;
            result = piece with { Core = new Century((int)t.Number, era), Era = era };
            return true;
        }
        // 19. century (German-style ordinal)
        if (t.Kind == TokenKind.Number && t.Digits <= 2 && Peek(1) is { Kind: TokenKind.Other, Text: "." } && Peek(2) is { Kind: TokenKind.CenturyWord })
        {
            _p += 3;
            var era = eraFirst ?? TakeEra() ?? Era.Ce;
            result = piece with { Core = new Century((int)t.Number, era), Era = era };
            return true;
        }
        // C19 | c14
        if (IsCenturyLetterAt(_p) && Peek(1) is { } n)
        {
            _p += 2;
            var era = eraFirst ?? TakeEra() ?? Era.Ce;
            result = piece with { Core = new Century((int)n.Number, era), Era = era };
            return true;
        }
        // s. XIV | siècle XIV | century XIV
        if ((t.Kind == TokenKind.CenturyWord || (t.Kind == TokenKind.Word && Lexicon.Normalise(t.Text) == "s")) && Peek(1) is { Kind: TokenKind.Roman or TokenKind.Ordinal } r && r.Number is >= 1 and <= 30)
        {
            _p += 2;
            var era = eraFirst ?? TakeEra() ?? Era.Ce;
            result = piece with { Core = new Century((int)r.Number, era), Era = era };
            return true;
        }
        // XVI secolo | XIX siècle (roman numeral before the century word)
        if (t.Kind == TokenKind.Roman && t.Number is >= 1 and <= 30 && Peek(1) is { Kind: TokenKind.CenturyWord })
        {
            _p += 2;
            var era = eraFirst ?? TakeEra() ?? Era.Ce;
            result = piece with { Core = new Century((int)t.Number, era), Era = era };
            return true;
        }
        return false;
    }

    private bool TryDeepTime(out DateExpression? expression)
    {
        expression = null;
        var t = _t[_p];
        var value = t.Value is decimal d ? d : (decimal)t.Number;
        var magnitude = Magnitude.Years;
        var i = _p + 1;
        if (Peek(1) is { Kind: TokenKind.Magnitude } m) { magnitude = (Magnitude)m.Value!; i++; }
        var next = i < _t.Count ? _t[i] : null;
        if (next is { Kind: TokenKind.Ago }) { _p = i + 1; expression = new DeepTime(value, magnitude); return true; }
        if (next is { Kind: TokenKind.Bp }) { _p = i + 1; expression = new DeepTime(value, magnitude, DeepTimeReference.BeforePresent1950); return true; }
        if (next is { Kind: TokenKind.Noise } && i + 1 < _t.Count && _t[i + 1].Kind is TokenKind.Ago or TokenKind.Bp)
        {
            expression = new DeepTime(value, magnitude, _t[i + 1].Kind == TokenKind.Bp ? DeepTimeReference.BeforePresent1950 : DeepTimeReference.Present);
            _p = i + 2;
            return true;
        }
        return false;
    }

    // ---- helpers ----------------------------------------------------------------------------------------------------

    private Token? Peek(int offset) => _p + offset < _t.Count ? _t[_p + offset] : null;

    private static bool IsCenturyLetter(Token t) => t.Kind is TokenKind.Hedge or TokenKind.Word && t.Text.TrimEnd('.').Equals("c", StringComparison.OrdinalIgnoreCase);

    /// <summary>"C19", "c. 14": the letter c followed by a small number that is not itself the start of something else ("c. 10 ka BP").</summary>
    private bool IsCenturyLetterAt(int index) =>
        index < _t.Count && IsCenturyLetter(_t[index])
        && index + 1 < _t.Count && _t[index + 1] is { Kind: TokenKind.Number } n && n.Digits <= 2 && !n.HasDecimal && n.Number is >= 1 and <= 30
        && (index + 2 >= _t.Count || _t[index + 2].Kind is not (TokenKind.Magnitude or TokenKind.Ago or TokenKind.Bp or TokenKind.DecadeSuffix or TokenKind.Month or TokenKind.Slash or TokenKind.Number));

    private bool IsYearShaped(Token t) => t.Kind == TokenKind.Number && !t.HasDecimal && t.Digits >= _profile.MinYearDigits && t.Digits <= 6;

    private Era? TakeEra()
    {
        if (_p < _t.Count && _t[_p].Kind == TokenKind.Era) { var e = (Era)_t[_p].Value!; _p++; return e; }
        return null;
    }

    private long YearOf(Token number, Era? eraFirst, out Era? era)
    {
        era = eraFirst ?? TakeEra();
        var value = number.Number;
        if (era == Era.Ce && value == 0) { _notes.Add("There is no year 0; '0 AD' is read as 1 BCE."); return 0; }
        return era is null ? value : ApplyEra(value, era.Value);
    }

    private static long ApplyEra(long value, Era era) => era == Era.Bce ? 1 - value : value;

    private static long Expand(Piece context, long twoDigits)
    {
        var start = context.Core switch { YearPoint y => y.Year, Decade d => d.Base, UnspecifiedDigits u => u.Minimum, _ => 0 };
        var century = start - start % 100;
        var candidate = century + twoDigits;
        return candidate >= start ? candidate : candidate + 100;
    }

    private bool TryWindowYear(Token t, out long year)
    {
        year = t.Number;
        if (t.Digits >= 3) return true;
        if (_profile.TwoDigitYearWindow is not { } window) return false;
        year = window - window % 100 + t.Number;
        if (year < window) year += 100;
        _flags.Add(DateFlag.TwoDigitYear);
        Penalise(0.1, $"The two-digit year '{t.Text}' was read as {year} within the profile's window.");
        return true;
    }

    private static DayPoint Day(long year, int month, int day) => new(year, month, day);

    private static Qualification Merge(Qualification? a, Qualification b) => a is null || a == b ? b : Qualification.ApproximateAndUncertain;

    private void SkipNoise()
    {
        while (_p < _t.Count && _t[_p].Kind is TokenKind.Noise or TokenKind.Comma) { if (_t[_p].Kind == TokenKind.Noise) Penalise(0.0, null); _p++; }
    }

    // "mid-1950s", "post-1945": a dash touching the word on both sides joins; "early - mid" is a range.
    private void SkipJoiner()
    {
        if (_p < _t.Count && _p > 0 && _t[_p].Kind == TokenKind.Dash && _p + 1 < _t.Count && _t[_p + 1].Kind != TokenKind.Dash
            && _t[_p].Start == _t[_p - 1].End && _t[_p + 1].Start == _t[_p].End) _p++;
    }

    private void Penalise(double amount, string? note)
    {
        _confidence = Math.Max(0.3, _confidence - amount);
        if (note is not null) _notes.Add(note);
    }

    private static bool IsPeriod(DateExpression e) => e switch { NamedPeriod => true, Qualified q => IsPeriod(q.Inner), Banded b => IsPeriod(b.Inner), _ => false };

    private static bool IsRestatement(DateExpression main, DateExpression other) =>
        main is NamedPeriod { Resolved: { } r } && (r == other || (r is Interval { Start: { } s, End: { } e } && other is Interval { Start: { } os, End: { } oe } && s == os && e == oe));

    private static DateExpression Hull(DateExpression a, DateExpression b)
    {
        DateExpression Start(DateExpression e) => e is Interval { Start: { } s } ? s : e;
        DateExpression End(DateExpression e) => e is Interval { End: { } en } ? en : e;
        var first = Start(a); var last = End(b);
        if (Order(first) > Order(last)) (first, last) = (Start(b), End(a));
        return new Interval(first, last);
    }

    private static long Order(DateExpression e) => e switch
    {
        YearPoint y => y.Year, MonthPoint m => m.Year, DayPoint d => d.Year, Decade d => d.FirstYear,
        Century c => c.Era == Era.Bce ? -100L * c.Number : 100L * (c.Number - 1), UnspecifiedDigits u => u.Minimum,
        Qualified q => Order(q.Inner), Banded b => Order(b.Inner), Relative r => Order(r.Inner), Lifecycle l => Order(l.Inner),
        Interval { Start: { } s } => Order(s), _ => long.MinValue,
    };
}
