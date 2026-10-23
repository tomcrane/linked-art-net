using System.Text;
using System.Text.RegularExpressions;
using LinkedArtNet.Dates;
using LinkedArtNet.Dates.Realisation;

namespace LinkedArtNet.Agents;

/// <summary>
/// Reads one agent string into its parts. The method is extraction, not a grammar: bracketed groups are taken off
/// and classified by what the profile says each bracket kind may hold; then, where the profile allows, trailing
/// comma segments are classified from the right until one is not a date, honorific, role or nationality phrase and
/// so belongs to the name; qualifiers are stripped from the ends of what remains; the date part goes to the dates
/// parser with <c>Intent.Pair</c>. Nothing here throws for bad input. Thread-safe; keep one per profile.
/// </summary>
public sealed partial class AgentStringParser
{
    public const string Version = "0.1.0";

    private readonly AgentProfile _profile;
    private readonly TimeSpanParser _dates;
    private readonly AgentTables _tables;
    private readonly string? _datesProfileNote;

    /// <param name="profile">The source's conventions; the built-in <c>default</c> when null.</param>
    /// <param name="datesProfiles">The dates parser's profile catalog, for the profile's <see cref="AgentProfile.DatesProfile"/>; only <c>default</c> is available when null.</param>
    public AgentStringParser(AgentProfile? profile = null, ProfileCatalog? datesProfiles = null, AgentTables? tables = null)
    {
        _profile = profile ?? AgentProfile.Default;
        _tables = tables ?? AgentTables.Default;
        ParseProfile datesProfile;
        if (_profile.DatesProfile is "default" or "") datesProfile = ParseProfile.Default;
        else if (datesProfiles is not null && datesProfiles.TryGet(_profile.DatesProfile, out var found)) datesProfile = found;
        else { datesProfile = ParseProfile.Default; _datesProfileNote = $"The dates profile '{_profile.DatesProfile}' is not available here; dates were read under 'default'."; }
        _dates = new TimeSpanParser(datesProfile, periods: Dates.Periods.PeriodOSource.ForRepository(null));
    }

    public static AgentStringParser Default { get; } = new();

    public AgentProfile Profile => _profile;

    // ---- entry -------------------------------------------------------------------------------------------------

    public ParsedAgent Parse(string? input)
    {
        var original = input ?? "";
        var notes = new List<string>();
        if (_datesProfileNote is not null) notes.Add(_datesProfileNote);
        var confidence = 0.95;
        var text = Clean(original);
        if (text.Length == 0) return Empty(original, "Empty input.");

        var honorifics = new List<string>();
        var dateParts = new List<string>();
        var roles = new List<AgentRole>();
        var nationalities = new List<AgentNationality>();
        var qualifiers = new HashSet<AgentQualifier>();
        var unconsumed = new List<string>();

        void Take(Segment segment, bool fromGroup)
        {
            switch (segment.Kind)
            {
                case SegmentKind.Date: dateParts.Add(segment.Text); break;
                case SegmentKind.Honorific: honorifics.Add(segment.Text); break;
                case SegmentKind.Role:
                    roles.Add((AgentRole)segment.Payload!);
                    if (((AgentRole)segment.Payload!).AatUri is null && !_tables.TryRole(segment.Text, out _)) { confidence -= 0.05; notes.Add($"'{segment.Text}' is read as a role but is not in the role table."); }
                    break;
                case SegmentKind.Qualifier: foreach (var q in (IEnumerable<AgentQualifier>)segment.Payload!) qualifiers.Add(q); break;
                case SegmentKind.Nationality:
                    var (ns, rs) = ((List<AgentNationality>, List<AgentRole>))segment.Payload!;
                    nationalities.AddRange(ns); roles.AddRange(rs);
                    break;
                default:
                    unconsumed.Add(segment.Text);
                    confidence -= fromGroup ? 0.1 : 0.15;
                    notes.Add($"Could not place \"{segment.Text}\".");
                    break;
            }
        }

        // 1. Bracketed groups, classified by what the profile lets each kind of bracket hold.
        var (core, groups) = ExtractGroups(text);
        foreach (var group in groups)
        {
            var allowed = group.Open switch { '[' => _profile.Brackets, '(' => _profile.Parentheses, _ => _profile.Braces };
            foreach (var segment in ClassifyGroup(group.Content, allowed)) Take(segment, fromGroup: true);
        }

        // 2. EAD "Surname; Forenames (dates); epithet": the semicolons are the structure.
        if (_profile.SemicolonForm && core.Contains(';'))
        {
            var parts = core.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            core = parts.Length >= 2 ? $"{parts[0]}, {parts[1]}" : parts[0];
            foreach (var epithet in parts.Skip(2))
            {
                var segment = Classify(epithet, AllMeanings, inGroup: false);
                Take(segment.Kind == SegmentKind.Unknown ? segment with { Kind = SegmentKind.Role, Payload = new AgentRole(epithet, AgentTables.Key(epithet), null, false) } : segment, fromGroup: true);
            }
        }

        // 3. Trailing comma segments: "Smith, John, Sir, 1866-1960, artist" gives up its tail to the right classifier.
        if (_profile.TrailingSegments)
        {
            var segments = SplitTopLevel(core, ',');
            var keep = segments.Count;
            for (var i = segments.Count - 1; i >= (_profile.InvertedNames ? 1 : 0); i--)
            {
                var segment = Classify(segments[i], AllMeanings, inGroup: false);
                // "Milton, John 1608-1674", "Gander, Ryan 1976-": a date segment that opens with name words is the forenames and the dates
                // with the comma between them dropped; the words go back to the name, the dates are the dates (2026-10-23, from headings
                // the reconciler had matched on the surname alone)
                if (segment.Kind == SegmentKind.Date && NameThenDate().Match(segments[i]) is { Success: true } split
                    && Classify(split.Groups["date"].Value, AllMeanings, inGroup: false) is { Kind: SegmentKind.Date } dateOnly)
                {
                    Take(dateOnly, fromGroup: false);
                    segments[i] = split.Groups["name"].Value.Trim();
                    keep = i + 1;
                    break;
                }
                if (segment.Kind == SegmentKind.Unknown) break;
                Take(segment, fromGroup: false);
                keep = i;
            }
            // A string with no name at all ("Dutch painter, 1606-1669"): the first segment too may be a description.
            if (keep == 1 && !_profile.NameRequired)
            {
                var first = Classify(segments[0], AllMeanings, inGroup: false);
                if (first.Kind != SegmentKind.Unknown) { Take(first, fromGroup: false); keep = 0; }
            }
            core = string.Join(", ", segments.Take(keep)).Trim();
        }

        // 4. Qualifiers wrapped round the name: "attributed to Rembrandt", "Rembrandt and others", "Anonymous".
        core = StripQualifiers(core, qualifiers, notes);

        // 5. Honorifics inside the name: "Sir John Smith", "Smith, Sir John". A body keeps its words: "Queen Victoria Museum" is
        //    not the Victoria Museum (the 2026-10-20 shadow matched it to one), nor "Sir John Soane's Museum" John Soane's.
        if (!IsCorporate(core)) core = StripHonorifics(core, honorifics);

        // 6. Name parts.
        string? name = core.Length == 0 ? null : core;
        string? family = null, given = null, sortName = name, displayName = name;
        if (name is not null && _profile.InvertedNames)
        {
            var comma = name.IndexOf(',');
            if (comma > 0 && name.IndexOf(',', comma + 1) < 0)
            {
                family = name[..comma].Trim();
                given = name[(comma + 1)..].Trim();
                if (given.Length == 0) { given = null; name = family; sortName = displayName = family; }
                else { sortName = $"{family}, {given}"; displayName = $"{given} {family}"; }
            }
        }

        // 7. Kind.
        var kind = AgentKind.Person;
        if (qualifiers.Contains(AgentQualifier.Anonymous) || qualifiers.Contains(AgentQualifier.Unknown)) kind = name is null ? AgentKind.Unknown : kind;
        if (name is not null && IsCorporate(name)) { kind = AgentKind.Group; notes.Add("A corporate body, by the words in its name."); }
        else if (name is null && _profile.NameRequired) { confidence -= 0.2; notes.Add("No name was found."); }

        // 8. Dates.
        string? dateText = dateParts.Count == 0 ? null : string.Join("; ", dateParts);
        TimeSpanParseResult? dates = null;
        if (dateText is not null)
        {
            dates = _dates.Parse(dateText, Intent.Pair);
            if (dates.Status == ParseStatus.Unparsed) { confidence -= 0.15; notes.Add($"The date part \"{dateText}\" could not be read."); }
            else if (dates.Confidence < 0.75) { confidence -= 0.05; notes.Add($"The date part was read with confidence {dates.Confidence:0.00}."); }
        }
        if (kind == AgentKind.Person && dates is { Kind: ResultKind.FormationDissolution }) kind = AgentKind.Group;

        if (name is null && kind != AgentKind.Unknown && dateText is null && roles.Count == 0 && nationalities.Count == 0 && qualifiers.Count == 0)
            return Empty(original, "Nothing recognisable in the text.");

        return new ParsedAgent(original, _profile.Name, kind, name, family, given, sortName, displayName, honorifics, dateText, dates,
            roles, nationalities, qualifiers, unconsumed.Count == 0 ? null : string.Join("; ", unconsumed), Math.Clamp(confidence, 0.1, 1), notes);
    }

    private ParsedAgent Empty(string input, string note) =>
        new(input, _profile.Name, AgentKind.Unknown, null, null, null, null, null, [], null, null, [], [], new HashSet<AgentQualifier>(), input.Length == 0 ? null : input, 0, [note]);

    // ---- segments -------------------------------------------------------------------------------------------------

    private enum SegmentKind { Date, Honorific, Role, Qualifier, Nationality, Unknown }

    private sealed record Segment(SegmentKind Kind, string Text, object? Payload = null);

    private static readonly IReadOnlyList<GroupMeaning> AllMeanings = [GroupMeaning.Dates, GroupMeaning.Honorific, GroupMeaning.Role, GroupMeaning.Qualifier, GroupMeaning.Nationality];

    /// <summary>A bracketed group may hold several things separated by commas or semicolons: "[Sir, 1866-1960]".</summary>
    private IEnumerable<Segment> ClassifyGroup(string content, IReadOnlyList<GroupMeaning> allowed)
    {
        if (!(content.Contains(',') || content.Contains(';'))) { yield return Classify(content, allowed, inGroup: true); yield break; }

        var parts = content.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        // Consecutive date-like parts were one date with a comma in it: "[b. 12 May, 1866]".
        var merged = new List<string>();
        foreach (var part in parts)
        {
            if (merged.Count > 0 && LooksLikeDate(part) && LooksLikeDate(merged[^1]) && !IsYearRange(part) && !IsYearRange(merged[^1]))
                merged[^1] = merged[^1] + ", " + part;
            else merged.Add(part);
        }
        var classified = merged.Select(part => Classify(part, allowed, inGroup: true)).ToList();
        if (classified.Any(c => c.Kind == SegmentKind.Unknown))
        {
            // "[b. 12 May, 1866]" as one date, or a long role phrase with a comma: the whole may read where the parts do not.
            var whole = Classify(content, allowed, inGroup: true);
            if (whole.Kind != SegmentKind.Unknown) { yield return whole; yield break; }
        }
        foreach (var c in classified) yield return c;
    }

    private Segment Classify(string raw, IReadOnlyList<GroupMeaning> allowed, bool inGroup)
    {
        var text = raw.Trim().Trim(',', ';').Trim();
        if (text.Length == 0) return new Segment(SegmentKind.Unknown, raw);

        if (allowed.Contains(GroupMeaning.Qualifier) && _tables.TryQualifier(text, out var qualifier))
            return new Segment(SegmentKind.Qualifier, text, new[] { qualifier });
        if (allowed.Contains(GroupMeaning.Honorific) && _tables.IsHonorific(text))
            return new Segment(SegmentKind.Honorific, text);
        if (allowed.Contains(GroupMeaning.Role) && _tables.TryRole(text, out var role))
            return new Segment(SegmentKind.Role, text, new AgentRole(text, role.Label, role.AatUri, role.IsOccupation));
        if (allowed.Contains(GroupMeaning.Nationality) && TryNationalityPhrase(text, out var nationalities, out var occupations))
            return new Segment(SegmentKind.Nationality, text, (nationalities, occupations));
        if (allowed.Contains(GroupMeaning.Dates) && LooksLikeDate(text))
        {
            var result = _dates.Parse(text, Intent.Pair);
            if (result.Status != ParseStatus.Unparsed && result.UnconsumedText is null)
                return new Segment(SegmentKind.Date, text);
        }
        // In a bracket whose profile says "role", a short phrase without digits is a role the table does not know yet.
        if (inGroup && allowed.Contains(GroupMeaning.Role) && !text.Any(char.IsDigit) && text.Split(' ').Length <= 5)
            return new Segment(SegmentKind.Role, text, new AgentRole(text, AgentTables.Key(text), null, false));
        return new Segment(SegmentKind.Unknown, text);
    }

    /// <summary>"Dutch painter", "Italian painter and architect", "French and Flemish engravers".</summary>
    private bool TryNationalityPhrase(string text, out List<AgentNationality> nationalities, out List<AgentRole> occupations)
    {
        nationalities = []; occupations = [];
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var i = 0;
        while (i < words.Length)
        {
            var matched = false;
            for (var span = Math.Min(_tables.LongestNationalityWords, words.Length - i); span >= 1 && !matched; span--)
            {
                var candidate = string.Join(' ', words.Skip(i).Take(span));
                if (_tables.TryNationality(candidate, out var entry))
                {
                    nationalities.Add(new AgentNationality(candidate, entry.Label, entry.AatUri));
                    i += span; matched = true;
                    if (i < words.Length && AgentTables.Key(words[i]) is "and" or "or" or "&") i++;
                }
            }
            if (!matched) break;
        }
        if (nationalities.Count == 0) return false;
        var rest = string.Join(' ', words.Skip(i));
        if (rest.Length == 0) return true;
        foreach (var chunk in Regex.Split(rest, @"\s*,\s*|\s+and\s+|\s*&\s*", RegexOptions.IgnoreCase).Where(c => c.Length > 0))
        {
            if (!_tables.TryRole(chunk, out var role)) { nationalities.Clear(); occupations.Clear(); return false; }
            occupations.Add(new AgentRole(chunk, role.Label, role.AatUri, role.IsOccupation));
        }
        return true;
    }

    // ---- name tidying ------------------------------------------------------------------------------------------

    private string StripQualifiers(string name, HashSet<AgentQualifier> qualifiers, List<string> notes)
    {
        var text = name;
        if (text.Length == 0) return text;
        if (_tables.TryQualifier(text, out var whole)) { qualifiers.Add(whole); return ""; }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // Leading phrase: "attributed to X", "circle of X", "after X".
        for (var span = Math.Min(_tables.LongestQualifierWords, words.Length - 1); span >= 1; span--)
        {
            if (_tables.TryQualifier(string.Join(' ', words.Take(span)), out var leading) && leading is not (AgentQualifier.Anonymous or AgentQualifier.Unknown))
            {
                qualifiers.Add(leading);
                words = words.Skip(span).ToArray();
                notes.Add($"'{leading}' qualifies the attribution.");
                break;
            }
        }
        // Trailing phrase: "X and others", "X et al.", "X?".
        for (var span = Math.Min(_tables.LongestQualifierWords, words.Length - 1); span >= 1; span--)
        {
            if (_tables.TryQualifier(string.Join(' ', words.Skip(words.Length - span)), out var trailing))
            {
                qualifiers.Add(trailing);
                words = words.Take(words.Length - span).ToArray();
                break;
            }
        }
        text = string.Join(' ', words).Trim();
        if (text.EndsWith('?')) { qualifiers.Add(AgentQualifier.Possibly); text = text.TrimEnd('?').TrimEnd(); }
        return text.Trim(',', ';', ' ');
    }

    private string StripHonorifics(string name, List<string> honorifics)
    {
        if (name.Length == 0) return name;
        var comma = name.IndexOf(',');
        if (_profile.InvertedNames && comma > 0)
        {
            var family = name[..comma];
            var givenWords = name[(comma + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            while (givenWords.Count > 1 && IsHonorificWord(givenWords[0])) { honorifics.Add(givenWords[0].TrimEnd('.')); givenWords.RemoveAt(0); }
            while (givenWords.Count > 1 && IsHonorificWord(givenWords[^1])) { honorifics.Add(givenWords[^1].TrimEnd('.')); givenWords.RemoveAt(givenWords.Count - 1); }
            return givenWords.Count == 0 ? family.Trim() : $"{family.Trim()}, {string.Join(' ', givenWords)}";
        }
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        while (words.Count > 1 && IsHonorificWord(words[0])) { honorifics.Add(words[0].TrimEnd('.')); words.RemoveAt(0); }
        return string.Join(' ', words);
    }

    /// <summary>
    /// A word of the name that is a title. A single letter never is, whatever the table says: "M" is in the table for the
    /// French Monsieur, but in "Larsen, Ruth M" or "J. Smith" it is an initial, and 2026-10-19's shadow run showed four
    /// strings matched to the wrong person for want of theirs.
    /// </summary>
    private bool IsHonorificWord(string word) => word.TrimEnd('.').Length > 1 && _tables.IsHonorific(word);

    private bool IsCorporate(string name)
    {
        var words = name.Split([' ', ',', '.', ';', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(_tables.IsCorporateWord)) return true;
        // an ampersand joins partners, or a founder and his son: a firm ("Crews & Son", "Edward Jas Carter & Banks"), never one person
        if (name.Contains('&')) return true;
        for (var i = 1; i < words.Length; i++)
            if (words[i].Equals("son", StringComparison.OrdinalIgnoreCase) && words[i - 1].Equals("and", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // ---- text mechanics ---------------------------------------------------------------------------------------

    private sealed record Group(char Open, string Content);

    /// <summary>Takes the top-level bracketed groups out of the text and returns what is left, tidied.</summary>
    private static (string Core, List<Group> Groups) ExtractGroups(string text)
    {
        var groups = new List<Group>();
        var core = new StringBuilder();
        var depth = 0; char open = '\0'; var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (depth == 0 && c is '[' or '(' or '{') { depth = 1; open = c; start = i + 1; continue; }
            if (depth > 0)
            {
                if (c == open) depth++;
                else if (c == Closer(open) && --depth == 0) { groups.Add(new Group(open, text[start..i].Trim())); core.Append(' '); }
                continue;
            }
            core.Append(c);
        }
        if (depth > 0) groups.Add(new Group(open, text[start..].Trim())); // an unclosed bracket runs to the end
        var tidy = Spaces().Replace(core.ToString(), " ").Trim();
        tidy = Regex.Replace(tidy, @"\s+,", ",").Trim(' ', ',', ';');
        return (tidy, groups);
    }

    private static char Closer(char open) => open switch { '[' => ']', '(' => ')', _ => '}' };

    private static List<string> SplitTopLevel(string text, char separator) =>
        text.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static bool LooksLikeDate(string text) => text.Any(char.IsDigit) || DateWords().IsMatch(text);

    private static bool IsYearRange(string text) => YearRange().IsMatch(text);

    private static string Clean(string input)
    {
        var t = input.Normalize(NormalizationForm.FormKC);
        t = t.Replace('‘', '\'').Replace('’', '\'').Replace('“', '"').Replace('”', '"').Replace('–', '-').Replace('—', '-');
        return Spaces().Replace(t, " ").Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\b(fl|b|d|c|ca|circa|active|flourished|floruit|born|died|century|cent|bc|bce|ad|ce|baptised|baptized|buried|n\.?d)\b\.?", RegexOptions.IgnoreCase)]
    private static partial Regex DateWords();

    [GeneratedRegex(@"^\s*-?\d{3,4}\s*-\s*\d{2,4}\s*$")]
    private static partial Regex YearRange();

    /// <summary>One or more capitalised words with no digit, none of them a date word, then the rest beginning with a digit or an opening bracket: "John 1608-1674", "John W. 1925-", "Ryan 1976-".</summary>
    [GeneratedRegex(@"^(?<name>(?:(?!(?:fl|b|d|c|ca|circa|active|flourished|floruit|born|died|baptised|baptized|buried|op)\b)\p{Lu}[\p{L}'.\-]*\s+)+)(?<date>[\d(\[].*)$")]
    private static partial Regex NameThenDate();
}
