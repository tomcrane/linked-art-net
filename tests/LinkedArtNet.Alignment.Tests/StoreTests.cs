using LinkedArtNet.Alignment.Import;
using Npgsql;

namespace LinkedArtNet.Alignment.Tests;

// One database for the class; each test uses its own dataset name so the tables can be shared without clashes.
[Collection("postgres")]
public class StoreTests(PostgresFixture pg)
{
    private static readonly IdentifierScheme Scheme = new("https://data.example.org/test");
    private static string Dataset() => "ds-" + Guid.NewGuid().ToString("N")[..8];

    [PostgresFact]
    public async Task Schema_applies_twice_without_complaint()
    {
        await pg.Store.InitialiseAsync();
        await pg.Store.InitialiseAsync();
        await using var cmd = pg.DataSource.CreateCommand("select max(version) from schema_version");
        Assert.Equal(1, (int)(await cmd.ExecuteScalarAsync())!);
    }

    [PostgresFact]
    public async Task Entities_are_minted_under_the_scheme_or_keep_a_given_id()
    {
        var ds = Dataset();
        var minted = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Person, "Grierson, Herbert John Clifford", Labels: ["Herbert Grierson", "Sir Herbert Grierson"],
            Lifespan: new Lifespan(new DateTimeOffset(1866, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(1866, 12, 31, 23, 59, 59, TimeSpan.Zero), new DateTimeOffset(1960, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(1960, 12, 31, 23, 59, 59, TimeSpan.Zero), "1866-1960"),
            Nationalities: ["Scottish"], Roles: ["http://vocab.getty.edu/aat/300025492"]), Scheme);
        Assert.StartsWith("https://data.example.org/test/person/", minted.Id);
        Assert.True(IdMinter.Looks(minted.Id.Split('/')[^1]));
        Assert.Equal(EntityStatus.Provisional, minted.Status);
        Assert.Equal(1866, minted.Lifespan!.BeginEarliest!.Value.Year);
        Assert.Equal("1866-1960", minted.Lifespan.Text);
        Assert.Equal(["Scottish"], minted.Nationalities);
        Assert.Empty(minted.Equivalents);

        var keptId = Scheme.Uri(EntityClass.Group, "kept-" + ds);
        var kept = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Tate Gallery", Id: keptId, Status: EntityStatus.Aligned), Scheme);
        Assert.Equal(keptId, kept.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Tate again", Id: kept.Id), Scheme));

        // Labels match by their normalised form, in any dataset or a given one, by class when asked.
        var byLabel = await pg.Store.FindEntitiesByLabelAsync(StringKey.Normalise("sir herbert grierson"));
        Assert.Contains(byLabel, e => e.Id == minted.Id);
        Assert.Empty(await pg.Store.FindEntitiesByLabelAsync(StringKey.Normalise("Herbert Grierson"), ds, EntityClass.Group));
        Assert.Single(await pg.Store.FindEntitiesByLabelAsync(StringKey.Normalise("Herbert Grierson"), ds, EntityClass.Person));
    }

    [PostgresFact]
    public async Task Equivalents_align_a_provisional_entity_and_are_found_by_uri()
    {
        var ds = Dataset();
        var e = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Person, "Rembrandt"), Scheme);
        var now = DateTimeOffset.UtcNow;
        await pg.Store.AddEquivalentAsync(e.Id, new Equivalent(AuthorityUris.Ulan + "500011051", "ulan", DecisionMethod.Vote, 0.92, now));
        await pg.Store.AddEquivalentAsync(e.Id, new Equivalent(AuthorityUris.Ulan + "500011051", "ulan", DecisionMethod.Human, 1.0, now)); // replaces
        await pg.Store.AddEquivalentAsync(e.Id, new Equivalent(AuthorityUris.Wikidata + "Q5598", "wikidata", DecisionMethod.Vote, 0.9, now));
        var again = (await pg.Store.GetEntityAsync(e.Id))!;
        Assert.Equal(EntityStatus.Aligned, again.Status);
        Assert.Equal(2, again.Equivalents.Count);
        Assert.Equal(DecisionMethod.Human, again.Equivalents.Single(q => q.Uri.EndsWith("500011051")).Method);
        var found = await pg.Store.FindEntitiesByEquivalentAsync(AuthorityUris.Wikidata + "Q5598");
        Assert.Equal(e.Id, Assert.Single(found).Id);

        var updated = again with { Label = "Rembrandt Harmenszoon van Rijn", Labels = ["Rembrandt"], Status = EntityStatus.Confirmed };
        await pg.Store.UpdateEntityAsync(updated);
        var read = (await pg.Store.GetEntityAsync(e.Id))!;
        Assert.Equal("Rembrandt Harmenszoon van Rijn", read.Label);
        Assert.Equal(EntityStatus.Confirmed, read.Status);
        Assert.Equal(2, read.Equivalents.Count); // untouched by the update
    }

    [PostgresFact]
    public async Task Strings_collect_once_per_context_and_count_occurrences()
    {
        var ds = Dataset();
        var first = await pg.Store.CollectAsync(ds, "library.author", "Smith, John, 1900-1980");
        var second = await pg.Store.CollectAsync(ds, "library.author", "Smith,  John, 1900–1980.", parsedJson: """{"name":"Smith, John"}""");
        var other = await pg.Store.CollectAsync(ds, "library.subject", "Smith, John, 1900-1980");
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(2, second.Occurrences);
        Assert.Equal("Smith, John, 1900-1980", second.Raw); // the first spelling is kept
        Assert.Contains("Smith, John", second.ParsedJson);
        Assert.True(second.LastSeen >= first.LastSeen);
        Assert.NotEqual(first.Id, other.Id);
        Assert.Equal(second.Id, (await pg.Store.FindSourceStringAsync(ds, "library.author", "smith, john, 1900-1980"))!.Id);
        await Assert.ThrowsAsync<ArgumentException>(() => pg.Store.CollectAsync(ds, "x", "   "));

        var unresolved = await pg.Store.UndecidedAsync(ds);
        Assert.Equal([first.Id, other.Id], unresolved.Select(s => s.Id));
    }

    [PostgresFact]
    public async Task Decisions_supersede_each_other_and_resolve_through_merges()
    {
        var ds = Dataset();
        var s = await pg.Store.CollectAsync(ds, "library.author", "Grierson, Herbert John Clifford [Sir, 1866-1960]");
        var a = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Person, "Grierson, Herbert"), Scheme);
        var b = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Person, "Grierson, Herbert John Clifford"), Scheme);
        await pg.Store.AddEquivalentAsync(b.Id, new Equivalent(AuthorityUris.Viaf + "1", "viaf", DecisionMethod.Vote, 0.9, DateTimeOffset.UtcNow));

        var d1 = await pg.Store.DecideAsync(s.Id, null, DecisionMethod.Vote, """{"candidates":[]}""", runId: "run-1");
        Assert.Null(d1.EntityId);
        Assert.Null(await pg.Store.ResolveAsync(ds, "library.author", s.Raw));
        Assert.DoesNotContain(await pg.Store.UndecidedAsync(ds), x => x.Id == s.Id); // looked at and left unresolved: not the reconciler's work until reopened
        Assert.Equal(1, (await pg.Store.CountsAsync(ds)).Unresolved); // but it counts as unresolved in the overview
        var d2 = await pg.Store.DecideAsync(s.Id, a.Id, DecisionMethod.Human, reviewer: "tomcrane");
        Assert.Equal(a.Id, (await pg.Store.DecisionForAsync(s.Id))!.EntityId);
        Assert.Equal(a.Id, (await pg.Store.ResolveAsync(ds, "library.author", s.Raw))!.Id);
        Assert.True(d2.Valid);

        // Merging a into b: the decision follows, a still resolves, b inherits nothing it lacked (a had no equivalents).
        await pg.Store.MergeAsync(a.Id, b.Id, "tomcrane");
        var aAfter = (await pg.Store.GetEntityAsync(a.Id))!;
        Assert.Equal(EntityStatus.Merged, aAfter.Status);
        Assert.Equal(b.Id, aAfter.MergedInto);
        Assert.Equal(b.Id, (await pg.Store.ResolveEntityAsync(a.Id))!.Id);
        Assert.Equal(b.Id, (await pg.Store.DecisionForAsync(s.Id))!.EntityId);
        Assert.Equal(b.Id, (await pg.Store.ResolveAsync(ds, "library.author", s.Raw))!.Id);
        Assert.DoesNotContain(await pg.Store.FindEntitiesByLabelAsync(StringKey.Normalise("Grierson, Herbert")), e => e.Id == a.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => pg.Store.MergeAsync(a.Id, b.Id, null)); // already merged
        await Assert.ThrowsAsync<ArgumentException>(() => pg.Store.MergeAsync(b.Id, b.Id, null));

        // Reopening: every string decided to b goes back to the reconciler.
        Assert.Equal(1, await pg.Store.InvalidateDecisionsForEntityAsync(b.Id));
        Assert.Null(await pg.Store.DecisionForAsync(s.Id));
        Assert.Contains(await pg.Store.UndecidedAsync(ds), x => x.Id == s.Id);
    }

    [PostgresFact]
    public async Task Overrides_prefer_the_context_and_fall_back_to_any()
    {
        var ds = Dataset();
        await pg.Store.PutOverrideAsync(new Override(ds, null, "tate", "https://data.example.org/test/group/pmc-tate-gallery", "pmc-lux/groups.json"));
        await pg.Store.PutOverrideAsync(new Override(ds, "library.subject", "tate", "https://data.example.org/test/concept/tate-as-subject", "curated"));
        Assert.Equal("https://data.example.org/test/concept/tate-as-subject", (await pg.Store.OverrideForAsync(ds, "library.subject", "tate"))!.Target);
        var any = (await pg.Store.OverrideForAsync(ds, "library.publisher", "tate"))!;
        Assert.Equal("https://data.example.org/test/group/pmc-tate-gallery", any.Target);
        Assert.Null(any.Context);
        Assert.Null(await pg.Store.OverrideForAsync(ds, "library.publisher", "tate modern"));
        await pg.Store.PutOverrideAsync(new Override(ds, null, "tate", "https://data.example.org/test/group/other", "pmc-lux/groups.json")); // replaces
        Assert.Equal("https://data.example.org/test/group/other", (await pg.Store.OverrideForAsync(ds, "x", "tate"))!.Target);
    }

    [PostgresFact]
    public async Task Authority_records_cache_by_uri_and_match_by_label()
    {
        var uri = AuthorityUris.Ulan + "500011051";
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(uri, "ulan", "Rembrandt van Rijn", ["Rembrandt", "Rembrandt Harmensz. van Rijn"], "Person",
            new Lifespan(new DateTimeOffset(1606, 1, 1, 0, 0, 0, TimeSpan.Zero), null, null, new DateTimeOffset(1669, 12, 31, 0, 0, 0, TimeSpan.Zero), "1606-1669"),
            [AuthorityUris.Wikidata + "Q5598", AuthorityUris.Viaf + "64013650"], """{"id":"500011051"}""", DateTimeOffset.UtcNow, "ulan-2026-09"));
        var read = (await pg.Store.GetAuthorityAsync(uri))!;
        Assert.Equal("Rembrandt van Rijn", read.Label);
        Assert.Equal(2, read.SameAs.Count);
        Assert.Equal(1606, read.Lifespan!.BeginEarliest!.Value.Year);
        Assert.Contains("500011051", read.DataJson);
        var found = await pg.Store.FindAuthoritiesByLabelAsync(StringKey.Normalise("rembrandt"), "ulan");
        Assert.Contains(found, a => a.Uri == uri);
        Assert.Empty(await pg.Store.FindAuthoritiesByLabelAsync(StringKey.Normalise("rembrandt"), "aat"));
        await pg.Store.PutAuthorityAsync(read with { Label = "Rembrandt (1606-1669)", SourceVersion = "ulan-2026-10" });
        Assert.Equal("ulan-2026-10", (await pg.Store.GetAuthorityAsync(uri))!.SourceVersion);
    }

    [PostgresFact]
    public async Task Runs_record_start_finish_and_counts()
    {
        var ds = Dataset();
        var run = await pg.Store.StartRunAsync(ds, """{"dataset":"x"}""", """[{"file":"a.xml","sha256":"00"}]""", "abc1234");
        Assert.StartsWith("run-", run.Id);
        Assert.Null(run.FinishedAt);
        Assert.Contains("a.xml", run.DumpsJson);
        await pg.Store.FinishRunAsync(run.Id, """{"read":10}""", "# Report");
        var done = (await pg.Store.GetRunAsync(run.Id))!;
        Assert.NotNull(done.FinishedAt);
        Assert.Equal("# Report", done.Report);
        Assert.Equal(run.Id, Assert.Single(await pg.Store.RunsAsync(ds)).Id);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => pg.Store.FinishRunAsync("run-nonesuch", null, null));

        await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "London"), Scheme);
        var s = await pg.Store.CollectAsync(ds, "place", "London");
        await pg.Store.CollectAsync(ds, "place", "Paris");
        await pg.Store.DecideAsync(s.Id, (await pg.Store.FindEntitiesByLabelAsync("london", ds)).Single().Id, DecisionMethod.ExactName);
        var counts = await pg.Store.CountsAsync(ds);
        Assert.Equal(new StoreCounts(1, 2, 1, 1, 0, counts.AuthorityRecords, 1), counts);
    }

    [PostgresFact]
    public async Task Pmc_lux_overrides_import_as_entities_and_overrides()
    {
        var ds = Dataset();
        var file = PmcLuxOverrides.Parse("""
            { "local_string_map": { "Tate": "pmc-tate-gallery", "Tate Gallery": "pmc-tate-gallery", "The Tate Gallery.": "pmc-tate-gallery", "Mystery Society": "pmc-mystery" },
              "authorities": [ { "identifier": "pmc-tate-gallery", "type": "Group", "label": "Tate Gallery", "viaf": "155600523", "ulan": "500301641" },
                               { "identifier": "pmc-unplaced", "type": "Group", "label": "Unplaced Society" } ] }
            """);
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        var result = await file.ImportAsync(pg.Store, scheme, ds, "pmc-lux/groups.json", EntityClass.Group);
        Assert.Equal(new PmcLuxImportResult(2, 0, 2, 4, 1), result);
        var tate = (await pg.Store.GetEntityAsync($"https://data.example.org/{ds}/group/pmc-tate-gallery"))!;
        Assert.Equal(EntityStatus.Aligned, tate.Status);
        Assert.Equal(2, tate.Equivalents.Count);
        Assert.All(tate.Equivalents, q => Assert.Equal(DecisionMethod.Override, q.Method));
        Assert.Equal(EntityStatus.Provisional, (await pg.Store.GetEntityAsync($"https://data.example.org/{ds}/group/pmc-unplaced"))!.Status);
        Assert.Equal(tate.Id, (await pg.Store.OverrideForAsync(ds, "library.publisher", StringKey.Normalise("The Tate Gallery.")))!.Target);
        Assert.Equal($"https://data.example.org/{ds}/group/pmc-mystery", (await pg.Store.OverrideForAsync(ds, "x", "mystery society"))!.Target);

        var again = await file.ImportAsync(pg.Store, scheme, ds, "pmc-lux/groups.json", EntityClass.Group);
        Assert.Equal(0, again.EntitiesAdded);
        Assert.Equal(2, again.EntitiesExisting);
        Assert.Equal(2, (await pg.Store.GetEntityAsync(tate.Id))!.Equivalents.Count);
    }

    [PostgresFact]
    public async Task Legacy_pmc_tables_import_with_their_identifiers()
    {
        var ds = Dataset();
        // The old tables, in the same test database, in the shape the pmc-2 code used.
        await using (var cmd = pg.DataSource.CreateCommand(
            """
            create table if not exists authorities (identifier text primary key, type text, label text, ulan text, aat text, lux text, loc text, viaf text, wikidata text, pmc text, tgn text);
            create table if not exists source_string_map (source text, string text, authority text, processed timestamptz, unreconciled_authority text);
            delete from authorities; delete from source_string_map;
            insert into authorities (identifier, type, label, ulan, viaf) values ('abcd2345', 'Person', 'Grierson, Herbert John Clifford', '500000001', '100');
            insert into authorities (identifier, type, label) values ('bcde3456', 'Group', 'Some Press');
            insert into authorities (identifier, type, label) values ('cdef4567', 'Oddity', 'Not a known type');
            insert into source_string_map (source, string, authority) values ('persauthorfull', 'Grierson, Herbert John Clifford [Sir, 1866-1960]', 'abcd2345');
            insert into source_string_map (source, string, authority) values ('persauthorfull', 'Grierson, H. J. C.', 'abcd2345');
            insert into source_string_map (source, string, authority) values ('publisher', 'Some Press', 'bcde3456');
            insert into source_string_map (source, string, unreconciled_authority) values ('publisher', 'Unknown Press', 'unrec_zzzz9999');
            insert into source_string_map (source, string, authority) values ('keywords', 'Dangling', 'nonesuch');
            """))
            await cmd.ExecuteNonQueryAsync();

        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        var result = await new LegacyPmcImporter(pg.DataSource).ImportAsync(pg.Store, scheme, ds);
        Assert.Equal(new LegacyImportResult(EntitiesAdded: 2, EntitiesExisting: 0, Equivalents: 2, SkippedForType: 1, Strings: 5, Decided: 3, Unresolved: 2, DanglingAuthority: 1), result);
        var grierson = (await pg.Store.ResolveAsync(ds, "persauthorfull", "Grierson, H. J. C."))!;
        Assert.Equal($"https://data.example.org/{ds}/person/abcd2345", grierson.Id);
        Assert.Equal(EntityStatus.Aligned, grierson.Status);
        Assert.Equal(DecisionMethod.Imported, (await pg.Store.DecisionForAsync((await pg.Store.FindSourceStringAsync(ds, "persauthorfull", "Grierson, H. J. C."))!.Id))!.Method);
        var unknown = (await pg.Store.FindSourceStringAsync(ds, "publisher", "Unknown Press"))!;
        Assert.Contains("unrec_zzzz9999", unknown.ParsedJson);
        Assert.Null(await pg.Store.DecisionForAsync(unknown.Id));
        Assert.Equal(EntityStatus.Provisional, (await pg.Store.GetEntityAsync($"https://data.example.org/{ds}/group/bcde3456"))!.Status);

        var again = await new LegacyPmcImporter(pg.DataSource).ImportAsync(pg.Store, scheme, ds);
        Assert.Equal(0, again.EntitiesAdded);
        Assert.Equal(3, again.Decided);
        Assert.Equal(2, (await pg.Store.FindSourceStringAsync(ds, "publisher", "Some Press"))!.Occurrences); // collecting twice counts twice; the decision is not duplicated
    }

    [PostgresFact]
    public async Task A_second_legacy_copy_reuses_entities_through_shared_authority_ids()
    {
        var ds = Dataset();
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        // The big run: two groups, two decided strings.
        await using (var cmd = pg.DataSource.CreateCommand(
            """
            create table if not exists authorities (identifier text primary key, type text, label text, ulan text, aat text, lux text, loc text, viaf text, wikidata text, pmc text, tgn text);
            create table if not exists source_string_map (source text, string text, authority text, processed timestamptz, unreconciled_authority text);
            delete from authorities; delete from source_string_map;
            insert into authorities (identifier, type, label, ulan, viaf) values ('big00001', 'Group', 'Brooklyn Museum of Art', '500267611', '265104231');
            insert into authorities (identifier, type, label, ulan) values ('big00002', 'Group', 'Arts Council of Great Britain', '500219164');
            insert into source_string_map (source, string, authority) values ('corpauthor', 'Brooklyn Museum', 'big00001');
            insert into source_string_map (source, string, authority) values ('corpauthor', 'Arts Council of Great Britain', 'big00002');
            """))
            await cmd.ExecuteNonQueryAsync();
        var first = await new LegacyPmcImporter(pg.DataSource).ImportAsync(pg.Store, scheme, ds);
        Assert.Equal(2, first.EntitiesAdded);

        // The later, smaller run: its own identifiers. One is the Brooklyn Museum again by VIAF with a different label and a
        // new string; one disagrees about a string the big run decided (and loses); one is new and referenced; one is new
        // and referenced by nothing.
        await using (var cmd = pg.DataSource.CreateCommand(
            """
            delete from authorities; delete from source_string_map;
            insert into authorities (identifier, type, label, viaf) values ('small001', 'Group', 'Brooklyn Museum', '265104231');
            insert into authorities (identifier, type, label, viaf) values ('small002', 'Group', 'Arts Council England', '143603950');
            insert into authorities (identifier, type, label) values ('small003', 'Group', 'Christie''s East (Firm)');
            insert into authorities (identifier, type, label, viaf) values ('small004', 'Group', 'Nobody Asked', '1');
            insert into source_string_map (source, string, authority) values ('corpauthor', 'Brooklyn Museum', 'small001');
            insert into source_string_map (source, string, authority) values ('corpauthor', 'Brooklyn Museum, N.Y.', 'small001');
            insert into source_string_map (source, string, authority) values ('corpauthor', 'Arts Council of Great Britain', 'small002');
            insert into source_string_map (source, string, authority) values ('corpauthor', 'Christie''s East (Firm)', 'small003');
            """))
            await cmd.ExecuteNonQueryAsync();
        var second = await new LegacyPmcImporter(pg.DataSource).ImportAsync(pg.Store, scheme, new LegacyImportOptions(ReuseByEquivalent: true, OnlyReferenced: true, DatasetVersion: "legacy-small"), ds);
        Assert.Equal(new LegacyImportResult(EntitiesAdded: 1, EntitiesExisting: 0, Equivalents: 0, SkippedForType: 0, Strings: 4, Decided: 4, Unresolved: 0, DanglingAuthority: 0, EntitiesReused: 1), second);

        var brooklyn = (await pg.Store.ResolveAsync(ds, "corpauthor", "Brooklyn Museum, N.Y."))!;
        Assert.Equal($"https://data.example.org/{ds}/group/big00001", brooklyn.Id); // the new string went to the big run's entity
        Assert.Contains("Brooklyn Museum", brooklyn.Labels); // and the small run's label joined it
        Assert.Equal("Brooklyn Museum of Art", brooklyn.Label);
        Assert.Null(await pg.Store.GetEntityAsync($"https://data.example.org/{ds}/group/small001"));
        Assert.Equal($"https://data.example.org/{ds}/group/big00002", (await pg.Store.ResolveAsync(ds, "corpauthor", "Arts Council of Great Britain"))!.Id); // the big run's answer stood
        Assert.Null(await pg.Store.GetEntityAsync($"https://data.example.org/{ds}/group/small002")); // the losing authority was never created
        Assert.Null(await pg.Store.GetEntityAsync($"https://data.example.org/{ds}/group/small004")); // nor the unreferenced one
        var christies = (await pg.Store.ResolveAsync(ds, "corpauthor", "Christie's East (Firm)"))!;
        Assert.Equal($"https://data.example.org/{ds}/group/small003", christies.Id);
        Assert.Equal("legacy-small", (await pg.Store.DecisionForAsync((await pg.Store.FindSourceStringAsync(ds, "corpauthor", "Christie's East (Firm)"))!.Id))!.DatasetVersion);
    }
}
