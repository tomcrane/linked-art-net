using LinkedArtNet.Alignment.Authorities;

namespace LinkedArtNet.Alignment.Tests;

/// <summary>The map's coordinates (the owner, 2026-11-08): TGN's strings in the record's data, a pull's numbers under "pulled", nothing otherwise.</summary>
public class CoordinatesTests
{
    private static AuthorityRecord Record(string? data) => new("http://vocab.getty.edu/tgn/1", "tgn", "x", [], "Place", null, [], data, DateTimeOffset.UtcNow, null);

    [Fact]
    public void Tgn_strings_and_pulled_numbers_are_read_and_nothing_else_is()
    {
        Assert.Equal((42.3584, -71.0598), Coordinates.Of(Record("""{"lat":"42.3584","long":"-71.0598","parentString":"Suffolk"}""")));
        Assert.Equal((51.5, -0.12), Coordinates.Of(Record("""{"ids":{},"pulled":{"lat":51.5,"long":-0.12}}""")));
        Assert.Equal((42.3584, -71.0598), Coordinates.Of(Record("""{"lat":"42.3584","long":"-71.0598","pulled":{"lat":51.5,"long":-0.12}}"""))); // the vocabulary's own first
        Assert.Null(Coordinates.Of(Record("""{"lat":"42.3584"}""")));
        Assert.Null(Coordinates.Of(Record("""{"pulled":{"lat":null,"long":null}}""")));
        Assert.Null(Coordinates.Of(Record("""{"ids":{}}""")));
        Assert.Null(Coordinates.Of(Record(null)));
        Assert.Null(Coordinates.Of(Record("not json")));
        Assert.Null(Coordinates.Of((AuthorityRecord?)null));
        Assert.Equal((51.5, -0.12), Coordinates.Of([null, Record("{}"), Record("""{"pulled":{"lat":51.5,"long":-0.12}}""")]));
    }
}
