using LinkedArtNet;
using LinkedArtNet.Parsers;
using LinkedArtNet.Vocabulary;
using PmcTransformer.Helpers;
using System.Xml.Linq;

namespace PmcTransformer.Library
{
    public static class Helpers
    {
        public static readonly XNamespace LibNS = "x-schema:EF-34074-Export.dtd";

        public static IEnumerable<string> LibStrings(this XElement record, string field)
        {
            return record.Elements(LibNS + field)
                .Select(el => el.Value)
                .Where(s => s.HasText());
        }

        public static bool ShouldSkipRecord(XElement record)
        {
            var id = record.Attribute("ID")!.Value;
            // "Missing record created by data verification program"
            if (id == "Q$") return true;

            var title = record.Attribute("title")!.Value;
            if (title.StartsWith("*"))
            {
                return true;
            }

            var medium = record.LibStrings("medium").Single();
            if (medium == "Journal") return true;

            var allClasses = record.LibStrings("class")
                .Select(s => s.ToUpperInvariant().Replace("(", "").Replace(")", ""))
                .ToList();

            if (allClasses.Contains("PHOTOGRAPHIC ARCHIVE"))
            {
                if (record.LibStrings("corpauthor").Any(ca => ca.StartsWith("Paul Mellon Centre")))
                {
                    // PMC Photo Archive will be dealt with separately
                    return true;
                }
            }
            if (allClasses.Contains("MISSING"))
            {
                return true;
            }
            if (allClasses.Contains("ORDERED"))
            {
                return true;
            }
            if (allClasses.Contains("UNAVAILABLE"))
            {
                return true;
            }
            if (allClasses.Contains("IN QUARANTINE"))
            {
                return true;
            }
            if (allClasses.Any(c => c.StartsWith("JOURNALS")))
            {
                return true;
            }
            return false;
        }

        public static void AddEdition(XElement record, LinguisticObject work, List<string> editionStatementsFromNotes)
        {
            // /referred_to_by[type=LinguisticObject,classified_as=EDITION_STMT]/value
            var edition = record.LibStrings("edition").SingleOrDefault();
            if (edition != null)
            {
                work.ReferredToBy ??= [];
                work.ReferredToBy.Add(
                    new LinguisticObject()
                        .WithContent(edition)
                        .WithClassifiedAs(Getty.EditionStatement)
                );
            }
            foreach (var statement in editionStatementsFromNotes)
            {
                // TODO: We may need a more subtle comparison.
                if (statement != edition)
                {
                    work.ReferredToBy ??= [];
                    work.ReferredToBy.Add(
                        new LinguisticObject()
                            .WithContent(statement)
                            .WithClassifiedAs(Getty.EditionStatement)
                    );
                }
            }
        }

        public static bool AddMedium(XElement record, LinguisticObject work, List<HumanMadeObject> hmos)
        {
            // Observed so far ALL records have exactly one medium.
            // /classified_as/id
            var medium = record.LibStrings("medium").Single();
            return ProcessMedium(work, hmos, medium);
        }

        public static bool ProcessMedium(LinguisticObject work, List<HumanMadeObject> hmos, string medium)
        {
            var mediumClassifier = Media.FromRecordValue(medium);
            if (mediumClassifier == (null, null))
            {
                // Image files
                return true;
            }
            else
            {
                var workMedium = mediumClassifier.Item1;
                if (workMedium != null)
                {
                    work.WithClassifiedAs(workMedium);
                }
                var hmoMedium = mediumClassifier.Item2;
                if (hmoMedium != null)
                {
                    foreach (var hmo in hmos)
                    {
                        hmo.WithClassifiedAs(hmoMedium);
                    }
                }
            }

            return false;
        }

        public static void AddCollation(XElement record, LinguisticObject work)
        {
            // collation
            // /referred_to_by[classified_as=COLLATION/value
            var collations = record.LibStrings("collation").ToList();
            if (collations.Count == 1)
            {
                // There is only ever none or one
                var collationStatement = new LinguisticObject()
                    .WithClassifiedAs(
                        Getty.AatType("Collations Statement", "300435452"),
                        Getty.AatType("Brief Text", "300418049"))
                    .WithContent(collations[0]);

                work.ReferredToBy ??= [];
                work.ReferredToBy.Add(collationStatement);
            }
        }

        public static List<string> GetClasses(XElement record)
        {
            // See notes (messy)
            // /identified_by[type=Identifier]/value   OR   /current_location/id
            // Distribution of class values:
            // Unfiltered        Filtered as linq below
            // 0: 0              15889
            // 1: 38863          26457
            // 2: 24481          21009
            // 3: 39             28
            // 4: 4              4
            // 5: 1              1
            // 9: 2              2
            // 12: 1             1
            string[] ignoredClasses = [
                "AUCTION CATALOGUES",
                "PMC SUPPORTED",
                "PMC PUBLICATION"
            ];
            var classes = record.LibStrings("class")
                .Where(v => !ignoredClasses.Contains(v))
                .Where(v => !v.StartsWith("IN PROCESS"))
                .Where(v => !v.StartsWith("YCBA"))
                .Where(v => !v.StartsWith("With Grants &"))
                .ToList();
            return classes;
        }

        public static void AddSeriesStatement(XElement record, LinguisticObject work)
        {
            // series - this is a statement
            // /referred_to_by[classified_as=???]/value
            var series = record.LibStrings("series").SingleOrDefault();
            var seriesno = record.LibStrings("seriesno").SingleOrDefault();
            // AAT 300417214
            // A Name of work with classification of series title 

            if (series != null)
            {
                if (seriesno != null)
                {
                    series += ", number " + seriesno;
                }
                work.IdentifiedBy ??= [];
                work.IdentifiedBy.Add(
                    new Name(series)
                        .WithClassifiedAs(Getty.AatType("Series title", "300417214")));
            }
        }

        public static int AddLanguage(HashSet<string> distinctLang, XElement record, LinguisticObject work)
        {
            // lng
            var language = record.LibStrings("lng").ToList();
            foreach (var l in language)
            {
                distinctLang.Add(l);
                var gettyLang = Language.GetLanguage(l);
                if (gettyLang != null)
                {
                    work.Language ??= [];
                    work.Language.Add(gettyLang);
                }
            }
            int langCount = language.Count;
            return langCount;
        }

        public static void AddAccessStatement(XElement record, LinguisticObject work)
        {
            // afilecsvx 
            // /referred_to_by[classified_as=ACCESS_STMT]/value
            List<string>? links = record.LibStrings("afilecsvx")
                .SingleOrDefault()?
                .Split("||", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(l => l.StartsWith("http"))
                .ToList();
            if (links != null)
            {
                foreach (var link in links)
                {
                    var spacePos = link.IndexOf(' ');
                    string? linkText = null;
                    string? linkHref = null;
                    if (spacePos > 0)
                    {
                        linkHref = link.Substring(0, spacePos);
                        linkText = link.Substring(spacePos + 1).TrimOuterBrackets();
                    }
                    else
                    {
                        linkHref = link;
                        linkText = link;
                    }
                    var html = $"""<span class="lux_data"><a href="{linkHref}">{linkText}</a></span>""";
                    var accessStatement = new LinguisticObject()
                        .WithContent(html)
                        .WithClassifiedAs(Getty.AccessStatement);
                    work.ReferredToBy ??= [];
                    work.ReferredToBy.Add(accessStatement);
                }
            }
        }
    }
}
