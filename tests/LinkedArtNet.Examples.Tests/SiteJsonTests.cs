namespace LinkedArtNet.Examples.Tests;

/// <summary>
/// The site's own JSON against the 1.0 schemas. A failure here is a linked.art problem to report upstream, not ours;
/// the ids listed in <see cref="KnownInvalid"/> are the ones already recorded in docs/upstream-issues.md.
/// </summary>
public class SiteJsonTests
{
    private static readonly HashSet<string> KnownInvalid = new(StringComparer.Ordinal)
    {
        "activity/exha/1", "activity/exhb/1", "event/christies_24477/1", "event/christies_24477_16/1", "event/early_roman/1",
        "group/14thc_italian/1",
        "object/letter/3", "object/nightwatch/1", "object/nightwatch/17", "object/nightwatch/18", "object/nightwatch/25",
        "object/nightwatch/26", "object/nightwatch/4", "object/nightwatch/41", "object/nightwatch/43", "object/nightwatch/70",
        "object/nightwatch/9", "object/nightwatch/support", "object/spring/15", "object/spring/back", "object/spring/support",
        "object/yiadom-boakye/1",
        "person/rembrandt/10", "person/rembrandt/4", "place/amsterdam/1",
        "provenance/2", "provenance/3", "provenance/4", "provenance/5", "provenance/coffin_promise/1", "provenance/fixme/1",
        "provenance/nuveen_commission/1", "provenance/sharp_sale/1", "provenance/transfer/1", "provenance/unknown_faure/1",
        "provenance/yuag_stora/1",
        "set/24477_16_objects/2", "text/koot_nightwatch/1",
    };

    public static TheoryData<string> CachedIds() =>
        new(Directory.GetFiles(TestPaths.CachedExamples, "*.json", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(TestPaths.CachedExamples, f).Replace('\\', '/')[..^".json".Length])
            .Order(StringComparer.Ordinal));

    [Theory]
    [MemberData(nameof(CachedIds))]
    public void Site_json_is_valid_against_the_schemas(string id)
    {
        var errors = ExampleTests.Schemas.Value.LeafErrors(File.ReadAllText(TestPaths.CachedFile(id)));
        if (KnownInvalid.Contains(id))
        {
            Assert.True(errors.Count > 0, $"{id} is now schema-valid on the site; remove it from KnownInvalid.");
            return;
        }
        Assert.True(errors.Count == 0, $"{id}:\n  " + string.Join("\n  ", errors));
    }
}
