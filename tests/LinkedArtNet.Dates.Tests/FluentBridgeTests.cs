namespace LinkedArtNet.Dates.Tests;

public class FluentBridgeTests
{
    [Fact]
    public void WithTimeSpan_sets_one_span_labelled_with_the_text()
    {
        var production = new Production().WithTimeSpan("c. 1642");
        Assert.Equal("c. 1642", production.TimeSpan!.Label);
        Assert.Equal("1642-01-01T00:00:00Z", production.TimeSpan.BeginOfTheBegin?.ToString());
        Assert.Equal("1642-12-31T23:59:59Z", production.TimeSpan.EndOfTheEnd?.ToString());
        Assert.Contains(DateFlag.Approximate, TimeSpanBridge.LastResult!.Flags);
    }

    [Fact]
    public void WithTimeSpan_keeps_an_unparseable_string_as_a_label_only_span()
    {
        var activity = new Activity().WithTimeSpan("sometime, nobody knows");
        Assert.Equal("sometime, nobody knows", activity.TimeSpan!.Label);
        Assert.Null(activity.TimeSpan.BeginOfTheBegin);
        Assert.Null(activity.TimeSpan.EndOfTheEnd);
        Assert.Equal(ParseStatus.Unparsed, TimeSpanBridge.LastResult!.Status);
    }

    [Fact]
    public void WithLifespan_sets_birth_and_death()
    {
        var rembrandt = new Person().WithLabel("Rembrandt").WithLifespan("1606-1669");
        Assert.Equal("1606-01-01T00:00:00Z", rembrandt.Born!.TimeSpan!.BeginOfTheBegin?.ToString());
        Assert.Equal("1669-12-31T23:59:59Z", rembrandt.Died!.TimeSpan!.EndOfTheEnd?.ToString());
        Assert.Equal("1606-1669", rembrandt.Born.TimeSpan.Label);
    }

    [Fact]
    public void WithLifespan_with_one_marked_date_sets_one_half()
    {
        var born = new Person().WithLifespan("b. 1850");
        Assert.NotNull(born.Born);
        Assert.Null(born.Died);

        var died = new Person().WithLifespan("d. 1920?");
        Assert.Null(died.Born);
        Assert.Equal("1920-12-31T23:59:59Z", died.Died!.TimeSpan!.EndOfTheEnd?.ToString());
    }

    [Fact]
    public void WithLifespan_records_floruit_as_professional_activity()
    {
        var master = new Person().WithLifespan("fl. 1480-1510");
        Assert.Null(master.Born);
        Assert.Null(master.Died);
        var activity = Assert.Single(master.CarriedOut!);
        Assert.Equal("1480-01-01T00:00:00Z", activity.TimeSpan!.BeginOfTheBegin?.ToString());
        Assert.Equal(Vocabulary.Aat.ProfessionalActivity.Id, activity.ClassifiedAs![0].Id);
    }

    [Fact]
    public void WithLifespan_leaves_an_unmarked_single_date_alone()
    {
        var person = new Person().WithLifespan("1850");
        Assert.Null(person.Born);
        Assert.Null(person.Died);
        Assert.Equal(ParseStatus.Partial, TimeSpanBridge.LastResult!.Status);
    }

    [Fact]
    public void WithExistence_sets_formation_and_dissolution()
    {
        var guild = new Group().WithExistence("1480-1510");
        Assert.Equal("1480-01-01T00:00:00Z", guild.FormedBy!.TimeSpan!.BeginOfTheBegin?.ToString());
        Assert.Equal("1510-12-31T23:59:59Z", guild.DissolvedBy!.TimeSpan!.EndOfTheEnd?.ToString());
    }

    [Fact]
    public void A_custom_parser_can_be_supplied()
    {
        var strict = new TimeSpanParser(Realisation.ParseProfile.Default with { Name = "strict", CenturyConvention = Realisation.CenturyConvention.Strict });
        var production = new Production().WithTimeSpan("19th century", strict);
        Assert.Equal("1801-01-01T00:00:00Z", production.TimeSpan!.BeginOfTheBegin?.ToString());
    }
}
