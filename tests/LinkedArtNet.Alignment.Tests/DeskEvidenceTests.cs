using LinkedArtNet.Alignment.Authorities;
using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

/// <summary>
/// The desk's evidence after the run (2026-11-07): the second pass keeps its reasons on a string it leaves queued, and a card knows which
/// project entity holds its record now, named with its placement.
/// </summary>
[Collection("postgres")]
public class DeskEvidenceTests(PostgresFixture pg)
{
    private const string A = "http://vocab.getty.edu/ulan/500900001", B = "http://vocab.getty.edu/ulan/500900002";

    [PostgresFact]
    public async Task A_place_with_no_coordinates_borrows_the_gazetteers_by_name_and_where_it_is_said_to_be()
    {
        // the owner's second of five, 2026-11-23: "Chatham (Kent)" was the one candidate not on its map. The gazetteer files Chatham
        // under Medway, not Kent, so the parents do not agree: the record of the name near a place called Kent is taken, the one at home
        // when Ontario has a Chatham near its own Kent too
        var x = "q" + string.Concat(Guid.NewGuid().ToString("N").Where(char.IsLetter).Take(7));
        static AuthorityRecord Tgn(string id, string label, string parents, double lat, double lon) =>
            new($"http://vocab.getty.edu/tgn/{id}", "tgn", label, [label], "Place", null, [], $"{{\"parentString\":\"{parents}\",\"lat\":{lat.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"long\":{lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}", DateTimeOffset.UtcNow, "test");
        await pg.Store.PutAuthorityAsync(Tgn(x + "1", "Chat" + x, "Medway, England, United Kingdom, Europe, World", 51.37, 0.53));
        await pg.Store.PutAuthorityAsync(Tgn(x + "2", "Chat" + x, "Ontario, Canada, North and Central America, World", 42.40, -82.18));
        await pg.Store.PutAuthorityAsync(Tgn(x + "3", "Kent" + x, "England, United Kingdom, Europe, World", 51.25, 0.75));
        await pg.Store.PutAuthorityAsync(Tgn(x + "4", "Kent" + x, "Ontario, Canada, North and Central America, World", 42.45, -82.20));
        await pg.Store.PutAuthorityAsync(Tgn(x + "5", "Dove" + x, "Kent" + x + ", England, United Kingdom, Europe, World", 51.13, 1.31));
        var scheme = new IdentifierScheme("https://data.example.org/bw" + x);
        var desk = new ReviewService(pg.Store, scheme, new Reconciler(pg.Store, scheme, [], ProjectManifest.PmcDefault.ToOptions()));
        var borrowed = (await desk.BorrowFromGazetteerAsync($"Chat{x} (Kent{x})", "Kent" + x))!;
        Assert.Equal((51.37, 0.53), (borrowed.Lat, borrowed.Long));
        Assert.True(borrowed.Approximate);
        Assert.Contains("Medway", borrowed.Via); Assert.Contains("km from Kent" + x, borrowed.Via);
        // parents that agree are taken as before, with no distance asked
        Assert.Contains("TGN's Dove" + x, (await desk.BorrowFromGazetteerAsync($"Dove{x} (Kent{x})", "Kent" + x))!.Via);
        // nothing of the name, or nothing near: nothing borrowed
        Assert.Null(await desk.BorrowFromGazetteerAsync("Nowhere" + x, "Kent" + x));
        Assert.Null(await desk.BorrowFromGazetteerAsync("Chat" + x, "Dove" + x + "zz"));
    }

    [PostgresFact]
    public async Task The_second_pass_keeps_its_reasons_on_a_string_it_leaves_queued_and_a_card_knows_its_records_holder()
    {
        var ds = "de-" + Guid.NewGuid().ToString("N")[..5];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        // two undated namesakes tie at 1.00: queued in the first pass and still queued in the second
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(A, "ulan", "Hall, Harry", ["Hall, Harry"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(B, "ulan", "Hall, Harry", ["Hall, Harry"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/names/n79045553", "loc", "Boston (Mass.)", ["Boston (Mass.)", "Boston"], "Place", null, [], null, DateTimeOffset.UtcNow, "test"));
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan"), new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());
        var desk = new ReviewService(pg.Store, scheme, reconciler);
        var hall = await pg.Store.CollectAsync(ds, "persauthorfull", "Hall, Harry");
        var report = await reconciler.RunAsync(ds);
        Assert.Equal(1, report.Queued);
        Assert.Equal(1, report.Reconsidered);
        var decision = (await pg.Store.DecisionForAsync(hall.Id))!;
        Assert.Equal(DecisionMethod.Queued, decision.Method);
        Assert.Contains("secondPass", decision.EvidenceJson!); // the second pass's own evidence stands, not the first pass's
        Assert.NotNull(decision.EntityId); // the placeholder unchanged
        Assert.Equal(DecisionMethod.Queued, (await pg.Store.DecisionForAsync(hall.Id))!.Method);

        // after the run, a person makes an entity hold record A: the card for the string knows, without "Look again"
        var before = (await desk.GetAsync(hall.Id))!;
        Assert.True(before.Holders is null || before.Holders.Count == 0);
        var boston = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "Boston", ["Boston"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(boston.Id, new Equivalent("http://id.loc.gov/authorities/names/n79045553", "loc", DecisionMethod.Human, 1, DateTimeOffset.UtcNow));
        var harry = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Person, "Harry Hall", ["Harry Hall"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(harry.Id, new Equivalent(A, "ulan", DecisionMethod.Human, 1, DateTimeOffset.UtcNow));
        var after = (await desk.GetAsync(hall.Id))!;
        Assert.NotNull(after.Holders);
        Assert.Equal(harry.Id, after.Holders![A].Entity.Id);
        Assert.False(after.Holders.ContainsKey(B));
        // a place entity's facts carry its placement, read from the LoC heading it holds
        var facts = (await desk.FactsAsync(boston.Id))!;
        Assert.Equal("Mass.", facts.Placement);
        Assert.Null((await desk.FactsAsync(harry.Id))!.Placement);
        // and its coordinates for the map, from a TGN record it holds (the owner, 2026-11-08), none without one
        Assert.Null(facts.Coordinates);
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://vocab.getty.edu/tgn/7013445", "tgn", "Boston", ["Boston"], "Place", null, [], """{"lat":"42.3584","long":"-71.0598","parentString":"Suffolk, Massachusetts, United States"}""", DateTimeOffset.UtcNow, "test"));
        await pg.Store.AddEquivalentAsync(boston.Id, new Equivalent("http://vocab.getty.edu/tgn/7013445", "tgn", DecisionMethod.Human, 1, DateTimeOffset.UtcNow));
        var placed = (await desk.FactsAsync(boston.Id))!.Coordinates;
        Assert.NotNull(placed);
        Assert.Equal(42.3584, placed.Lat, 4); Assert.Equal(-71.0598, placed.Long, 4);
        Assert.False(placed.Approximate);
        // a record without coordinates stands at its TGN parent, said to be approximate (Windsor Castle in England, the owner, 2026-11-08)
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://vocab.getty.edu/tgn/1100372", "tgn", "Windsor Castle", ["Windsor Castle"], "Place", null, [], """{"broader":"http://vocab.getty.edu/tgn/7011919","parentString":"Windsor, Windsor and Maidenhead, England"}""", DateTimeOffset.UtcNow, "test"));
        Assert.Null(await Coordinates.ResolveAsync(pg.Store, await pg.Store.GetAuthorityAsync("http://vocab.getty.edu/tgn/1100372"))); // the parent unknown yet
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://vocab.getty.edu/tgn/7011919", "tgn", "Windsor", ["Windsor"], "Place", null, [], """{"lat":"51.4833","long":"-0.6","parentString":"Windsor and Maidenhead, England"}""", DateTimeOffset.UtcNow, "test"));
        var castle = await Coordinates.ResolveAsync(pg.Store, await pg.Store.GetAuthorityAsync("http://vocab.getty.edu/tgn/1100372"));
        Assert.NotNull(castle);
        Assert.True(castle.Approximate);
        Assert.Equal("Windsor", castle.Near);
        Assert.Equal(51.4833, castle.Lat, 4);
        var windsor = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "Windsor Castle", ["Windsor Castle"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(windsor.Id, new Equivalent("http://vocab.getty.edu/tgn/1100372", "tgn", DecisionMethod.Human, 1, DateTimeOffset.UtcNow));
        Assert.Equal("Windsor", (await desk.FactsAsync(windsor.Id))!.Coordinates?.Near);
    }
}
