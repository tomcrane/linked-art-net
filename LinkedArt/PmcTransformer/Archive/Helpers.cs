using LinkedArtNet;
using LinkedArtNet.Vocabulary;
using PmcTransformer.Helpers;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PmcTransformer.Archive
{
    public static class Helpers
    {
        public static IEnumerable<string> ArcStrings(this XElement record, string field)
        {
            return record.Elements(field)
                .Select(el => el.Value)
                .Where(s => s.HasText());
        }

        public static void SetClassifiedAs(string? level, LinkedArtObject? laSet, HumanMadeObject? laItem)
        {
            // All non HMO levels should have archive. Then can have additional ones.
            laSet?.WithClassifiedAs(Getty.Archives);
            switch (level)
            {
                case "Item":
                    laItem!.WithClassifiedAs(Getty.ArchivalMaterials);
                    break;
                case "File":
                case "file":  // see BS/4/7/3
                case " File": // see BS/5/1/852
                case "File ": // see NWS/1/5
                    // no specific classification
                    break;
                case "Sub-Sub Series":
                    // no specific classification
                    break;
                case "Sub-Series":
                    laSet!.WithClassifiedAs(Getty.ArchivalSubGrouping);
                    break;
                case "Series":
                    laSet!.WithClassifiedAs(Getty.ArchivalGrouping);
                    break;
                case "Sub-Collection":
                    // no specific classification
                    break;
                case "Collection":
                    laSet!.WithClassifiedAs(Getty.Collection);
                    break;

                default:
                    throw new Exception("Unknown level " + level);
            }
        }

        public static bool ShouldSkipRecord(XElement record)
        {
            var mgtSubGroup = record.ArcStrings("MgtSubGroup").SingleOrDefault();
            if (mgtSubGroup == "INSTITUTIONAL RECORDS")
            {
                return true; // 235 instances of this at time of writing
            }

            var accessStatus = record.ArcStrings("AccessStatus").SingleOrDefault();
            if (accessStatus != "Open")
            {
                return true; // 0 instances of this at time of writing
            }

            var catalogueStatus = record.ArcStrings("CatalogueStatus").SingleOrDefault();
            if (catalogueStatus != "Catalogued")
            {
                return true; // 78 instances of this at time of writing
            }

            var level = record.ArcStrings("Level").SingleOrDefault();
            if (string.IsNullOrWhiteSpace(level))
            {
                return true; // 2 instances of this at time of writing
                // DAVETEST1
                // QJTH/1
            }



            return false;
        }

        public static void ProcessAltRefNo(XElement record, LinkedArtObject laObj)
        {

            var altRef = record.ArcStrings("AltRefNo").SingleOrDefault();
            if(string.IsNullOrWhiteSpace(altRef))
            {
                return;
            }

            string sPattern = @"^S[0-9]*$";
            var sMatch = Regex.Match(altRef, sPattern, RegexOptions.IgnoreCase);
            if(sMatch.Success)
            {
                laObj.AttributedBy ??= [];
                laObj.AttributedBy.Add(
                    new Activity(Types.AttributeAssignment)
                    {
                        IdentifiedBy = [
                            new Name("Related Library Record")
                                .WithClassifiedAs(Getty.DisplayTitle, Getty.BriefText)
                        ],
                        Assigned = [
                            new LinguisticObject()
                                .WithId($"{Identity.LibraryLinguistic}{altRef.ToUpperInvariant()}")
                        ]
                    }
                );
                return;
            }

            string platePattern = @"Plate[^\s]*\s(.*)";
            var pMatch = Regex.Match(altRef, platePattern, RegexOptions.IgnoreCase); 
            if(pMatch.Success) 
            {
                laObj.ReferredToBy ??= [];
                laObj.ReferredToBy.Add(
                    new LinguisticObject()
                        .WithClassifiedAs(Getty.AatType("Related Material", "300444119"), Getty.BriefText)
                        .WithContent(altRef)
                );
            }

            return;
        }
    }
}
