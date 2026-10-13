namespace LinkedArtNet.Dates;

/// <summary>
/// Free text straight onto the model: <c>production.WithTimeSpan("c. 1642")</c>, <c>person.WithLifespan("1606-1669")</c>,
/// <c>group.WithExistence("fl. 1480-1510")</c>. Lives here, not in the model library, so the model has no parser
/// dependency. The original string is always kept as the TimeSpan's label; when nothing can be parsed the TimeSpan
/// carries only the label, so the information is not lost, and <see cref="LastResult"/> says why.
/// </summary>
public static class TimeSpanBridge
{
    [ThreadStatic] private static TimeSpanParseResult? _last;

    /// <summary>The result of the most recent bridge call on this thread, for callers that want the confidence or notes.</summary>
    public static TimeSpanParseResult? LastResult => _last;

    /// <summary>Parses <paramref name="text"/> and sets the TimeSpan; one span covering everything the text denotes.</summary>
    public static T WithTimeSpan<T>(this T temporal, string text, TimeSpanParser? parser = null) where T : TemporalEntity
    {
        var result = (parser ?? TimeSpanParser.Default).Parse(text, Intent.Single);
        _last = result;
        temporal.TimeSpan = result.TimeSpan ?? LabelOnly(text);
        return temporal;
    }

    /// <summary>
    /// "1606-1669", "b. 1850", "d. 1920", "1606?-1669": sets <see cref="Person.Born"/> and/or <see cref="Person.Died"/>.
    /// A single unmarked date is not a lifespan and sets nothing; check <see cref="LastResult"/>.
    /// </summary>
    public static Person WithLifespan(this Person person, string text, TimeSpanParser? parser = null)
    {
        var result = (parser ?? TimeSpanParser.Default).Parse(text, Intent.Pair);
        _last = result;
        if (result.Kind == ResultKind.Floruit)
        {
            // Active dates are not a lifespan; record them as the period of professional activity instead.
            person.CarriedOut ??= [];
            person.CarriedOut.Add(new Activity { TimeSpan = result.TimeSpan ?? LabelOnly(text) }.WithClassifiedAs(Vocabulary.Aat.ProfessionalActivity));
            return person;
        }
        if (result.Begin is { } begin) { person.Born ??= new Birth(); person.Born.TimeSpan = begin; }
        if (result.End is { } end) { person.Died ??= new Death(); person.Died.TimeSpan = end; }
        return person;
    }

    /// <summary>"1480-1510", "fl. 1480-1510", "founded 1850": sets <see cref="Group.FormedBy"/> and/or <see cref="Group.DissolvedBy"/>.</summary>
    public static Group WithExistence(this Group group, string text, TimeSpanParser? parser = null)
    {
        var result = (parser ?? TimeSpanParser.Default).Parse(text, Intent.Pair);
        _last = result;
        if (result.Begin is { } begin) { group.FormedBy ??= new Formation(); group.FormedBy.TimeSpan = begin; }
        if (result.End is { } end) { group.DissolvedBy ??= new Dissolution(); group.DissolvedBy.TimeSpan = end; }
        return group;
    }

    private static LinkedArtTimeSpan LabelOnly(string text) => new() { Label = text };
}
