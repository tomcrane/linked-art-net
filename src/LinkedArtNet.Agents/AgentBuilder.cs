using LinkedArtNet.Dates;
using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Agents;

/// <summary>
/// Linked Art records from a <see cref="ParsedAgent"/>: a <see cref="Person"/> or <see cref="Group"/> with the name
/// (and its given and family parts when known), the lifespan or activity dates, nationality and occupation
/// classifications. The role the agent played for a record ("preface by") is not a property of the person and is
/// left to the caller's mapper. Honorifics and qualifiers are reported on the parse, not modelled here.
/// </summary>
public static class AgentBuilder
{
    /// <summary>A Person or Group by <see cref="ParsedAgent.Kind"/>; a Person when the kind is unknown.</summary>
    public static LinkedArtObject ToActor(this ParsedAgent agent, string? id = null) =>
        agent.Kind == AgentKind.Group ? ToGroup(agent, id) : ToPerson(agent, id);

    public static Person ToPerson(this ParsedAgent agent, string? id = null)
    {
        var person = new Person();
        if (id is not null) person.WithId(id);
        Names(person, agent);
        if (agent.Dates is { } dates)
        {
            if (dates.Kind == ResultKind.Floruit && dates.TimeSpan is not null)
            {
                person.CarriedOut ??= [];
                person.CarriedOut.Add(new Activity { TimeSpan = dates.TimeSpan }.WithClassifiedAs(Aat.ProfessionalActivity));
            }
            else
            {
                if (dates.Begin is { } begin) { person.Born ??= new Birth(); person.Born.TimeSpan = begin; }
                if (dates.End is { } end) { person.Died ??= new Death(); person.Died.TimeSpan = end; }
                if (dates.Begin is null && dates.End is null && dates.TimeSpan is not null && dates.Kind == ResultKind.Single)
                {
                    // One date on a person with no marker: recorded as the period they were active rather than guessed as a birth.
                    person.CarriedOut ??= [];
                    person.CarriedOut.Add(new Activity { TimeSpan = dates.TimeSpan }.WithClassifiedAs(Aat.ProfessionalActivity));
                }
            }
        }
        foreach (var nationality in agent.Nationalities.Where(n => n.AatUri is not null))
            person.WithClassifiedAs(Aat.NationalityTerm(nationality.AatUri!, nationality.Label));
        foreach (var role in agent.Roles.Where(r => r.IsOccupation && r.AatUri is not null))
            person.WithClassifiedAs(Aat.Term(role.AatUri!, role.Label).WithClassifiedAs(Aat.Occupation));
        return person;
    }

    public static Group ToGroup(this ParsedAgent agent, string? id = null)
    {
        var group = new Group();
        if (id is not null) group.WithId(id);
        Names(group, agent);
        if (agent.Dates is { } dates)
        {
            if (dates.Begin is { } begin) { group.FormedBy ??= new Formation(); group.FormedBy.TimeSpan = begin; }
            if (dates.End is { } end) { group.DissolvedBy ??= new Dissolution(); group.DissolvedBy.TimeSpan = end; }
            if (dates.Begin is null && dates.End is null && dates.TimeSpan is not null)
            {
                group.CarriedOut ??= [];
                group.CarriedOut.Add(new Activity { TimeSpan = dates.TimeSpan }.WithClassifiedAs(Aat.ProfessionalActivity));
            }
        }
        foreach (var nationality in agent.Nationalities.Where(n => n.AatUri is not null))
            group.WithClassifiedAs(Aat.NationalityTerm(nationality.AatUri!, nationality.Label));
        return group;
    }

    private static void Names(LinkedArtObject actor, ParsedAgent agent)
    {
        var label = agent.DisplayName ?? agent.Name ?? agent.Input;
        actor.WithLabel(label);
        var content = agent.SortName ?? agent.Name;
        if (content is null) return;
        actor.WithName(content, name =>
        {
            name.WithClassifiedAs(Aat.PrimaryName);
            if (agent.GivenNames is not null && agent.FamilyName is not null)
                name.Part =
                [
                    new Name { Content = agent.GivenNames }.WithClassifiedAs(Aat.GivenName),
                    new Name { Content = agent.FamilyName }.WithClassifiedAs(Aat.FamilyName),
                ];
        });
        if (agent.DisplayName is not null && agent.DisplayName != content) actor.WithName(agent.DisplayName);
    }
}
