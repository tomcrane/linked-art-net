using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

/// <summary>
/// The class a string is read as first, which a minted entity takes (2026-11-27, from the desk run of 2026-11-25: 241 corporate authors
/// and 1,453 publishers minted as persons). The name parser says "person" when it finds no word of a body; that is its default and not a
/// finding, so the field's own order stands unless the text shows a person. And several publishers in one field are a list.
/// </summary>
[Collection("postgres")]
public class ClassOrderTests(PostgresFixture pg)
{
    [PostgresFact]
    public void A_body_field_reads_a_body_unless_the_text_shows_a_person()
    {
        var reconciler = new Reconciler(pg.Store, new IdentifierScheme("https://data.example.org/co"), [], ProjectManifest.PmcDefault.ToOptions());
        EntityClass First(string context, string raw) => reconciler.Read(new SourceString(0, "co", context, raw, StringKey.Normalise(raw), null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1)).Classes[0];

        Assert.Equal(EntityClass.Group, First("corpauthor", "Lulworth Castle"));
        Assert.Equal(EntityClass.Group, First("corpauthor", "The Hunterian"));
        Assert.Equal(EntityClass.Group, First("publisher", "Shire Books"));
        Assert.Equal(EntityClass.Group, First("publisher", "W H Allen"));
        Assert.Equal(EntityClass.Group, First("publisher", "Christopher Helm"));
        Assert.Equal(EntityClass.Group, First("corpauthor", "Messrs Robins"));
        // the text shows a person: an honorific, the catalogue's inverted name with a role, dates
        Assert.Equal(EntityClass.Person, First("corpauthor", "Mr Stamp"));
        Assert.Equal(EntityClass.Person, First("publisher", "Taride, Alphonse (publisher)"));
        Assert.Equal(EntityClass.Person, First("publisher", "Heubach, Jean Pierre [1736-1799] (publisher)"));
        // a field of persons, and a dated heading among subjects, are as before
        Assert.Equal(EntityClass.Person, First("persauthorfull", "Powell, Kenneth (author)"));
        Assert.Equal(EntityClass.Person, First("keywords", "Turner, Daniel, fl 1782-1817"));
        Assert.Equal(EntityClass.Person, First("keywords", "Morgan, John, 1823-1886"));
        Assert.Equal(EntityClass.Type, First("keywords", "Castles"));

        // several publishers in one field are a list of them; one with a trailing semicolon is one
        var list = reconciler.Read(new SourceString(0, "co", "publisher", "Tate ; Ipswich Borough Council", "tate ; ipswich borough council", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1));
        Assert.Equal(PartsShape.List, list.Shape);
        Assert.Equal(["Tate", "Ipswich Borough Council"], list.Parts);
        Assert.Null(reconciler.Read(new SourceString(0, "co", "publisher", "North West Pattern Book Project;", "north west pattern book project;", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1)).Parts);
    }
}
