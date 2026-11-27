using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

/// <summary>Finding 112 (2026-11-07): the hyphen inside a heading, doubled by the 2024 cleaning and split as a separator.</summary>
[Collection("postgres")]
public class HeadingResplitTests(PostgresFixture pg)
{
    [PostgresFact]
    public async Task A_hyphen_inside_a_word_or_a_known_name_is_not_a_separator()
    {
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/subjects/sh85106186", "lcsh", "Pre-Raphaelitism", ["Pre-Raphaelitism"], "Type", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/names/n79041977", "loc", "New-York Historical Society", ["New-York Historical Society"], "Group", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/names/n85076990", "loc", "Kit-Cat Club (London, England)", ["Kit-Cat Club (London, England)", "Kit-Cat Club"], "Group", null, [], null, DateTimeOffset.UtcNow, "test"));
        var resplit = new HeadingResplit(pg.Store);

        async Task Is(string heading, string rule, params string[] parts)
        {
            var (got, r) = await resplit.SplitAsync(heading);
            Assert.Equal(parts, got);
            Assert.Equal(rule, r);
        }
        // (a) lower-case after the separator is a hyphen, except the catalogue's lower-case chronological subdivision
        await Is("Wood--carving--England--Devon", "a", "Wood-carving", "England", "Devon");
        await Is("Still--life painting--20th century", "a", "Still-life painting", "20th century");
        await Is("All Hallows by--the--Tower (Church : London, England)", "a", "All Hallows by-the-Tower (Church : London, England)");
        await Is("Barton--upon--Humber (England)--Antiquities", "a", "Barton-upon-Humber (England)", "Antiquities");
        await Is("Architecture, Domestic--England--to 1500", "", "Architecture, Domestic", "England", "to 1500");
        await Is("Art--Bio--bibliography", "a", "Art", "Bio-bibliography");
        // a bound prefix joins the next part (the splitter's own rule, widened)
        await Is("Anglo--Saxons--Exhibitions", "a", "Anglo-Saxons", "Exhibitions");
        await Is("Counter--Reformation--England", "a", "Counter-Reformation", "England");
        await Is("Pre--Raphaelitism--England--Exhibitions", "a", "Pre-Raphaelitism", "England", "Exhibitions");
        // 2026-11-27, from the desk run of 2026-11-25: a prefix at the end of a longer piece, a "--" inside a bracket, a piece that is a bracket
        await Is("Civilization, Anglo--Saxon--Exhibitions", "a", "Civilization, Anglo-Saxon", "Exhibitions");
        await Is("Buchanan, J, active mid--19th century", "a", "Buchanan, J, active mid-19th century");
        await Is("Musée Alpin (Chamonix--Mont--Blanc, France)", "a", "Musée Alpin (Chamonix-Mont-Blanc, France)");
        await Is("Beecroft Art Gallery (Westcliff--on--Sea)--Exhibitions", "a", "Beecroft Art Gallery (Westcliff-on-Sea)", "Exhibitions");
        await Is("Wye, River (Wales and England--Early works to 1800", "", "Wye, River (Wales and England", "Early works to 1800");
        await Is("Jonah--(Biblical prophet)--Art", "a", "Jonah (Biblical prophet)", "Art");
        await Is("Biennale di Venezia--(50th : 2003)", "a", "Biennale di Venezia (50th : 2003)");
        await Is("Smith & Co--Exhibitions", "", "Smith & Co", "Exhibitions");
        // (b) a one-word head whose join is a label an index holds with the hyphen; a plain heading stands
        await Is("New--York Historical Society (New York, New York, USA)", "b", "New-York Historical Society (New York, New York, USA)");
        await Is("Kit--Cat Club (London)--Portraits", "b", "Kit-Cat Club (London)", "Portraits");
        await Is("Art--16th century--England", "", "Art", "16th century", "England");
        await Is("Drury--Lowe family", "", "Drury", "Lowe family"); // a surname the indexes do not hold: the split stands, for the desk

        // the dry run over a dataset: a heading split today stands; one whose parts were made by an older split (the store's, from
        // the first runs before finding 4) is listed with its parts as stored and as today's rules read it
        var ds = "rs-" + Guid.NewGuid().ToString("N")[..5];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "lcsh"), new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());
        var art = await pg.Store.CollectAsync(ds, "keywords", "Art--16th century--England");
        await reconciler.RunAsync(ds);
        var stale = await pg.Store.CollectAsync(ds, "keywords", "Wood--carving--England");
        var wood = await pg.Store.CollectAsync(ds, "keywords:head", "Wood");
        var carving = await pg.Store.CollectAsync(ds, "keywords:sub", "carving");
        var england = await pg.Store.CollectAsync(ds, "keywords:sub", "England");
        await pg.Store.LinkPartAsync(stale.Id, 0, wood.Id, "head"); await pg.Store.LinkPartAsync(stale.Id, 1, carving.Id, "sub"); await pg.Store.LinkPartAsync(stale.Id, 2, england.Id, "sub");
        var concept = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Type, "Wood--carving--England", ["Wood--carving--England"]), scheme);
        await pg.Store.DecideAsync(stale.Id, concept.Id, DecisionMethod.Composite);
        // and a heading re-split with fewer parts kept its old last row beyond the new count: a leftover, not a rule
        var leftover = await pg.Store.CollectAsync(ds, "keywords:sub", "England");
        await pg.Store.LinkPartAsync(art.Id, 3, leftover.Id, "sub");
        var report = await resplit.DryRunAsync(ds);
        Assert.Equal(2, report.Read);
        Assert.Equal(2, report.Changes.Count);
        var change = report.Changes.Single(c => c.Heading.Id == stale.Id);
        Assert.Equal(["Wood", "carving", "England"], change.OldParts);
        Assert.Equal(["Wood-carving", "England"], change.NewParts);
        Assert.Equal("a", change.Rule);
        var extra = report.Changes.Single(c => c.Heading.Id == art.Id);
        Assert.Equal("leftover", extra.Rule);
        Assert.Equal(["Art", "16th century", "England", "England"], extra.OldParts);
        Assert.Equal(["Art", "16th century", "England"], extra.NewParts);

        // the reconciler's own reading joins a known hyphenated head (rule (b)), and reads the heading whole when nothing is left to subdivide
        var nyhs = await pg.Store.CollectAsync(ds, "keywords", "New--York Historical Society (New York, New York, USA)");
        var kitcat = await pg.Store.CollectAsync(ds, "keywords", "Kit--Cat Club (London)--Portraits");
        await reconciler.RunAsync(ds);
        var nyhsDecision = (await pg.Store.DecisionForAsync(nyhs.Id))!;
        Assert.NotEqual(DecisionMethod.Composite, nyhsDecision.Method);
        Assert.Contains((await pg.Store.GetEntityAsync(nyhsDecision.EntityId!))!.Equivalents, q => q.Uri == "http://id.loc.gov/authorities/names/n79041977");
        Assert.Equal(["Kit-Cat Club (London)", "Portraits"], (await pg.Store.PartsAsync(kitcat.Id)).Select(p => p.Raw));

        // the repair: the stale heading split again, the leftover row gone, and a stale one-part heading read whole
        var stale2 = await pg.Store.CollectAsync(ds, "keywords", "Palais--Royal (Paris, France)");
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/names/n80052580", "loc", "Palais-Royal (Paris, France)", ["Palais-Royal (Paris, France)"], "Place", null, [], null, DateTimeOffset.UtcNow, "test"));
        var palais = await pg.Store.CollectAsync(ds, "keywords:head", "Palais");
        var royal = await pg.Store.CollectAsync(ds, "keywords:sub", "Royal (Paris, France)");
        await pg.Store.LinkPartAsync(stale2.Id, 0, palais.Id, "head"); await pg.Store.LinkPartAsync(stale2.Id, 1, royal.Id, "sub");
        var concept2 = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Type, "Palais--Royal (Paris, France)", ["Palais--Royal (Paris, France)"]), scheme);
        await pg.Store.DecideAsync(stale2.Id, concept2.Id, DecisionMethod.Composite);
        var run = await pg.Store.StartRunAsync(ds);
        var applied = await resplit.ApplyAsync(ds, reconciler, run.Id);
        Assert.Equal(3, applied.DryRun.Changes.Count);
        Assert.Equal(1, applied.Whole);
        Assert.Equal(["Wood-carving", "England"], (await pg.Store.PartsAsync(stale.Id)).Select(p => p.Raw));
        Assert.Equal(["Art", "16th century", "England"], (await pg.Store.PartsAsync(art.Id)).Select(p => p.Raw));
        Assert.Equal(DecisionMethod.Composite, (await pg.Store.DecisionForAsync(stale.Id))!.Method);
        Assert.Contains("resplit", (await pg.Store.DecisionForAsync(stale.Id))!.EvidenceJson!);
        Assert.Empty(await pg.Store.PartsAsync(stale2.Id));
        var whole = (await pg.Store.DecisionForAsync(stale2.Id))!;
        Assert.NotEqual(DecisionMethod.Composite, whole.Method);
        Assert.Equal(run.Id, whole.RunId);
        Assert.Contains("\"carving\"", string.Join(" ", applied.Orphans)); // the old sub no heading carries now, reported
        Assert.Empty(await resplit.DryRunAsync(ds).ContinueWith(t => t.Result.Changes)); // nothing left to change

        // the fragments: listed, then deleted with their decisions, their placeholders gone; the parts still carried stay
        var contexts = HeadingResplit.PartContexts(ProjectManifest.PmcDefault.ToOptions());
        Assert.Contains("keywords:head", contexts); Assert.Contains("keywords:sub", contexts);
        var (listed, _, _) = await resplit.RemoveOrphanPartsAsync(ds, contexts, apply: false);
        Assert.Contains(listed, o => o.Id == carving.Id);
        Assert.Contains(listed, o => o.Id == palais.Id);
        Assert.DoesNotContain(listed, o => o.Id == england.Id);
        var (_, deleted, _) = await resplit.RemoveOrphanPartsAsync(ds, contexts, apply: true);
        Assert.Equal(listed.Count, deleted);
        Assert.Null(await pg.Store.DecisionForAsync(carving.Id));
        Assert.Empty((await resplit.RemoveOrphanPartsAsync(ds, contexts, apply: false)).Orphans);
        Assert.NotNull(await pg.Store.DecisionForAsync(england.Id));

        // a heading read whole since (the desk run of 2026-11-25: "Pre" on 193 records, its headings long decided whole) keeps the rows of
        // its old split, which the dry run over compounds never reads: its fragments are listed as carried by no compound, and go
        var wholeNow = await pg.Store.CollectAsync(ds, "keywords", "Pre--Raphaelitism");
        var pre = await pg.Store.CollectAsync(ds, "keywords:head", "Pre");
        var raph = await pg.Store.CollectAsync(ds, "keywords:sub", "Raphaelitism");
        await pg.Store.LinkPartAsync(wholeNow.Id, 0, pre.Id, "head"); await pg.Store.LinkPartAsync(wholeNow.Id, 1, raph.Id, "sub");
        await pg.Store.LinkPartAsync(wholeNow.Id, 2, england.Id, "sub"); // a part other headings carry: its row goes, the string stays
        await pg.Store.DecideAsync(wholeNow.Id, concept.Id, DecisionMethod.ExactSingle); // decided whole, by an earlier run
        Assert.Equal(3, (await pg.Store.PartsAsync(wholeNow.Id)).Count);
        var (fragments, _, _) = await resplit.RemoveOrphanPartsAsync(ds, contexts, apply: false);
        Assert.Equal([pre.Id, raph.Id], fragments.Select(f => f.Id).Order());
        Assert.Equal(3, (await pg.Store.PartsAsync(wholeNow.Id)).Count); // the listing writes nothing
        await resplit.RemoveOrphanPartsAsync(ds, contexts, apply: true);
        Assert.Empty(await pg.Store.PartsAsync(wholeNow.Id));
        // and a reconcile clears such rows itself, so they do not gather again
        var again = await pg.Store.CollectAsync(ds, "keywords", "Pre--Raphaelitism in literature");
        var pre2 = await pg.Store.CollectAsync(ds, "keywords:head", "Pre");
        await pg.Store.LinkPartAsync(again.Id, 0, pre2.Id, "head");
        await reconciler.RunAsync(ds);
        Assert.NotEqual(DecisionMethod.Composite, (await pg.Store.DecisionForAsync(again.Id))!.Method);
        Assert.Empty(await pg.Store.PartsAsync(again.Id));
        await resplit.RemoveOrphanPartsAsync(ds, contexts, apply: true);
        Assert.Null(await pg.Store.DecisionForAsync(pre.Id));
        Assert.NotNull(await pg.Store.DecisionForAsync(england.Id));
        Assert.NotEmpty(await pg.Store.PartsAsync(art.Id)); // a compound's rows are not touched
    }

    /// <summary>A hyphenated name no rule or index can tell ("Drury--Lowe family" beside "Abbeys--England") is said once at the desk, kept in the store, and read joined wherever it stands (2026-11-27).</summary>
    [PostgresFact]
    public async Task A_name_joined_at_the_desk_is_read_joined_wherever_it_stands()
    {
        Assert.Equal("Louis-François", HeadingResplit.NameAcross("Guiguer, Louis", "François"));
        Assert.Equal("Water-Colours", HeadingResplit.NameAcross("Royal Society of Painters in Water", "Colours (Great Britain)"));
        Assert.Equal("Chamonix-Mont", HeadingResplit.NameAcross("Musée Alpin (Chamonix", "Mont"));
        Assert.Equal("Lennox-Boyd", HeadingResplit.NameAcross("Lennox", "Boyd, Christopher A."));
        IReadOnlySet<string> names = new HashSet<string> { "drury-lowe", "water-colours", "wallraf-richartz", "wallraf-richartz-museum" };
        Assert.Equal(["Drury-Lowe family"], HeadingResplit.JoinNamed(["Drury", "Lowe family"], names));
        Assert.Equal(["Royal Society of Painters in Water-Colours (Great Britain)", "Exhibitions"], HeadingResplit.JoinNamed(["Royal Society of Painters in Water", "Colours (Great Britain)", "Exhibitions"], names));
        Assert.Equal(["Wallraf-Richartz-Museum", "Catalogs"], HeadingResplit.JoinNamed(["Wallraf", "Richartz", "Museum", "Catalogs"], names));
        Assert.Equal(["Abbeys", "England"], HeadingResplit.JoinNamed(["Abbeys", "England"], names));

        var ds = "hn-" + Guid.NewGuid().ToString("N")[..5];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "lcsh"), new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());
        var resplit = new HeadingResplit(pg.Store);
        var family = await pg.Store.CollectAsync(ds, "keywords", "Drury--Lowe family");
        var man = await pg.Store.CollectAsync(ds, "keywords", "Drury--Lowe, P J B--Art collections");
        var other = await pg.Store.CollectAsync(ds, "keywords", "Abbeys--England");
        await reconciler.RunAsync(ds);
        var drury = (await pg.Store.PartsAsync(family.Id))[0];
        Assert.Equal("Drury", drury.Raw);
        Assert.Equal(2, (await pg.Store.ParentsAsync(drury.Id)).Count);

        // the card of the half offers the name, read from its headings; the other half's card offers the same
        var offered = await resplit.ProposedJoinsAsync(drury);
        Assert.Equal("Drury-Lowe", Assert.Single(offered).Name);
        Assert.Equal(2, offered[0].Headings.Count);
        Assert.Equal("Drury-Lowe", Assert.Single(await resplit.ProposedJoinsAsync((await pg.Store.PartsAsync(family.Id))[1])).Name);
        Assert.Equal("Abbeys-England", Assert.Single(await resplit.ProposedJoinsAsync((await pg.Store.PartsAsync(other.Id))[0])).Name); // offered, never assumed

        // said once: both headings are read again, the family whole and the man's with its subdivision, and the halves go
        Assert.Equal(2, await resplit.JoinNameAsync(ds, "Drury-Lowe", "tester", reconciler));
        Assert.Empty(await pg.Store.PartsAsync(family.Id));
        Assert.NotEqual(DecisionMethod.Composite, (await pg.Store.DecisionForAsync(family.Id))!.Method);
        Assert.Equal(["Drury-Lowe, P J B", "Art collections"], (await pg.Store.PartsAsync(man.Id)).Select(p => p.Raw));
        Assert.Null(await pg.Store.GetSourceStringAsync(drury.Id));
        Assert.Equal(["Abbeys", "England"], (await pg.Store.PartsAsync(other.Id)).Select(p => p.Raw));
        Assert.Equal("tester", Assert.Single(await pg.Store.HyphenatedNamesAsync(ds)).Reviewer);
        // a heading met later is read joined from the start, and the pass over the headings finds nothing to change
        var later = await pg.Store.CollectAsync(ds, "keywords", "Drury--Lowe family--Art collections");
        await reconciler.RunAsync(ds);
        Assert.Equal(["Drury-Lowe family", "Art collections"], (await pg.Store.PartsAsync(later.Id)).Select(p => p.Raw));
        Assert.Empty((await resplit.DryRunAsync(ds)).Changes);

        // taken back: the headings are split at the mark again
        Assert.Equal(3, await resplit.UnjoinNameAsync(ds, "Drury-Lowe", reconciler));
        Assert.Equal(["Drury", "Lowe, P J B", "Art collections"], (await pg.Store.PartsAsync(man.Id)).Select(p => p.Raw));
        Assert.Empty(await pg.Store.HyphenatedNamesAsync(ds));
    }
}
