using LinkedArtNet.Dates.Realisation;
using LinkedArtNet.Dates.Text;

namespace LinkedArtNet.Dates.Tests;

public class MultilingualTests
{
    private static readonly TimeSpanParser Multilingual = new(ProfileCatalog.Load(TestPaths.Profiles)["multilingual"]);

    [Fact]
    public void Every_lexicon_loads_and_merges()
    {
        foreach (var code in new[] { "en", "fr", "de", "it", "es", "nl", "la" })
        {
            var lexicon = Lexicon.Load(code);
            Assert.Equal(code, lexicon.Language);
            Assert.NotEmpty(lexicon.Months);
            Assert.True(lexicon.MaxPhraseWords >= 1);
        }
        var merged = Lexicon.Merge([Lexicon.Load("en"), Lexicon.Load("fr")]);
        Assert.Equal("en+fr", merged.Language);
        Assert.Equal(5, merged.Months["mai"]);
        Assert.Equal(Qualification.Approximate, merged.Hedges["vers"]);
    }

    [Theory]
    // French
    [InlineData("XIVe siècle", "century 14")]
    [InlineData("vers 1850", "1850~")]
    [InlineData("fin du XIXe siècle", "end century 19")]
    [InlineData("seconde moitié du XVIIe siècle", "h2 century 17")]
    [InlineData("entre 1870 et 1890", "1870/1890")]
    [InlineData("12 mars 1850", "1850-03-12")]
    [InlineData("vers 500 av. J.-C.", "-0499~")]
    [InlineData("né en 1606", "born 1606")]
    [InlineData("s.d.", "undated")]
    // German
    [InlineData("um 1850", "1850~")]
    [InlineData("19. Jahrhundert", "century 19")]
    [InlineData("Anfang des 19. Jahrhunderts", "beginning century 19")]
    [InlineData("2. Hälfte 18. Jh.", "h2 century 18")]
    [InlineData("zwischen 1870 und 1890", "1870/1890")]
    [InlineData("12. März 1850", "1850-03-12")]
    [InlineData("500 v. Chr.", "-0499")]
    [InlineData("geb. 1606", "born 1606")]
    [InlineData("o.J.", "undated")]
    // Italian
    [InlineData("secolo XIV", "century 14")]
    [InlineData("sec. XIV", "century 14")]
    [InlineData("metà del XVI secolo", "mid century 16")]
    [InlineData("fine del XIX secolo", "end century 19")]
    [InlineData("tra il 1870 e il 1890", "1870/1890")]
    // Spanish
    [InlineData("siglo XVI", "century 16")]
    [InlineData("s. XVI", "century 16")]
    [InlineData("hacia 1850", "1850~")]
    [InlineData("finales del siglo XIX", "end century 19")]
    [InlineData("primera mitad del siglo XVII", "h1 century 17")]
    // Dutch
    [InlineData("19e eeuw", "century 19")]
    [InlineData("eind 19e eeuw", "end century 19")]
    [InlineData("omstreeks 1850", "1850~")]
    [InlineData("eerste helft 17e eeuw", "h1 century 17")]
    [InlineData("12 maart 1850", "1850-03-12")]
    // Latin
    [InlineData("saec. XIV", "century 14")]
    [InlineData("saec. XIV ex.", "end century 14")]
    [InlineData("s. xiv med.", "mid century 14")]
    [InlineData("anno 1485", "1485")]
    [InlineData("obiit 1669", "died 1669")]
    [InlineData("sine anno", "undated")]
    public void Reads_other_languages(string text, string canonical)
    {
        var result = Multilingual.Parse(text);
        Assert.Equal(canonical, result.Canonical);
        Assert.Equal(text, result.TimeSpan?.Label ?? text);
    }

    [Fact]
    public void English_wins_a_conflict_between_languages()
    {
        // "mars" is March in French; "mar" is March in English too. "ver" is Latin spring but nothing in English.
        Assert.Equal("1850-03", Multilingual.Parse("mars 1850").Canonical);
        Assert.Equal("1850-21", Multilingual.Parse("ver 1850").Canonical);
    }
}
