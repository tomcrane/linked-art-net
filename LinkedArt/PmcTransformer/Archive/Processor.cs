using LinkedArtNet;
using LinkedArtNet.Parsers;
using LinkedArtNet.Vocabulary;
using System.Text.Json;
using System.Xml.Linq;

namespace PmcTransformer.Archive
{
    public class Processor
    {
        internal static void ProcessArchives(XDocument xArchive, XDocument xAuthorities)
        {
            var archiveByGuid = new Dictionary<string, LinkedArtObject>();
            var archiveByRefNo = new Dictionary<string, LinkedArtObject>();
            var creatorDict = new Dictionary<string, List<string>>();
            var relatedNameDict = new Dictionary<string, List<string>>();

            foreach (var record in xArchive.Root!.Elements())
            {
                if (Helpers.ShouldSkipRecord(record))
                {
                    continue;
                }

                var id = record.ArcStrings("RecordID").Single();            // a GUID
                var refNo = record.ArcStrings("RefNo").Single();            // e.g., PMC/A/2
                var level = record.ArcStrings("Level").SingleOrDefault();   // Item, Series etc
                var title = record.ArcStrings("Title").Distinct().Single(); // EKW/1/167 has duplicated title

                bool isItem = level == "Item";
                LinkedArtObject? laSet = isItem ? null : new LinkedArtObject(Types.Set);
                HumanMadeObject? laItem = isItem ? new HumanMadeObject() : null;
                LinkedArtObject laObj = (laSet ?? laItem)!;
                LinkedArtObject? parent = null;
                LinkedArtObject? parentRef = null;

                archiveByGuid[id] = laObj;
                archiveByRefNo[refNo] = laObj;

                (string sortRefNo, string? parentRefNo) = Helpers.GetRefNoVariants(refNo); 
                if(parentRefNo != null)
                {
                    // This assumes the export XML walks DOWN the hierarchy
                    if (archiveByRefNo.ContainsKey(parentRefNo))
                    {
                        // LBN/4/3/5 and XAPO/2/2/15 have no parents atm
                        parent = archiveByRefNo[parentRefNo];
                        parentRef = new LinkedArtObject(Types.Set)
                            .WithId(parent.Id)
                            .WithLabel(parent.Label);
                        laObj.MemberOf = [parentRef];
                    }
                }

                laObj.WithContext().WithId($"{Identity.ArchiveRecord}{id}");
                laObj.IdentifiedBy = [
                    new Identifier(refNo).WithClassifiedAs(Getty.RecordIdentifiers),
                    new Name($"{refNo} - {title}").AsPrimaryName(),
                    Identifier.SortValue(sortRefNo, parentRef)
                ];


                Helpers.SetClassifiedAs(level, laSet, laItem);
                Helpers.ProcessAltRefNo(record, laObj);
                Helpers.ProcessDate(record, laObj);

                foreach (var creator in record.ArcStrings("CreatorName"))
                {
                    creatorDict.AddToListForKey(creator, id);
                }

                // statements/descriptions
                Helpers.SimpleStatement(record, laObj, "Extent", Getty.DimensionStatement);
                Helpers.SimpleStatement(record, laObj, "AdminHistory", Getty.AdministrativeHistory);
                Helpers.SimpleStatement(record, laObj, "CustodialHistory", Getty.ProvenanceStatement);
                Helpers.ProcessDescription(record, laObj); // See Stock Number TODO
                Helpers.SimpleStatement(record, laObj, "Accruals", Getty.Accruals);
                Helpers.SimpleStatement(record, laObj, "Arrangement", Getty.ArrangementDescription);
                Helpers.SimpleStatement(record, laObj, "AccessConditions", Getty.AccessStatement);
                Helpers.SimpleStatement(record, laObj, "RelatedMaterial", Getty.RelatedMaterial);
                Helpers.SimpleStatement(record, laObj, "PublnNote", Getty.GeneralNote);
                Helpers.ProcessThumbnailAndDescription(record, laObj);

                foreach (var creator in record.ArcStrings("RelatedNameCode"))
                {
                    relatedNameDict.AddToListForKey(creator, id);
                }
                var relatedNames = record.ArcStrings("RelatedName");
                var relatedNameRelationShips = record.ArcStrings("RelatedNameRelationship");
                var relatedNameDecription = record.ArcStrings("RelatedNameDecription");
                if(relatedNames.Any() || relatedNameRelationShips.Any() || relatedNameDecription.Any()) 
                {
                    Console.WriteLine(refNo);
                }
            }

            var AuthorityMa
            // Then /created_by/carried_out_by

            foreach(var kvp in Helpers.thumbnailTextCounts)
            {
                Console.WriteLine(kvp.Key + ": " + kvp.Value);
            }

            Sample(archiveByRefNo, 1000, true);
        }


        private static void Sample(
            Dictionary<string, LinkedArtObject> archiveByRefNo,
            int interval, bool writeToDisk)
        {
            List<string> pleaseDump = [
                "WGC/1/1/150",
                "WR/109",
                "APO/1/19/1",
                "ONM/2/52"
            ];
            var options = new JsonSerializerOptions { WriteIndented = true, };
            int count = 0;
            foreach (var obj in archiveByRefNo)
            {
                if (count % interval == 0 || pleaseDump.Contains(obj.Key))
                {
                    var generatedJson = JsonSerializer.Serialize(obj.Value, options);
                    Console.WriteLine(generatedJson);

                    if (writeToDisk)
                    {
                        var json = JsonSerializer.Serialize(obj.Value, options);
                        var guid = obj.Value.Id.LastPathElement();
                        File.WriteAllText($"../../../output/archive/{guid}.json", json);
                    }
                }
                count++;
            }
        }
    }
}
