using LinkedArtNet;
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

                // get parent and do the Archive sort value and parent thing here

                laObj.WithContext().WithId($"{Identity.ArchiveRecord}{id}");
                laObj.IdentifiedBy = [
                    new Identifier(refNo).WithClassifiedAs(Getty.RecordIdentifiers),
                    new Name($"{refNo} - {title}").AsPrimaryName(),

                ];

                archiveByGuid[id] = laObj;
                archiveByRefNo[refNo] = laObj;

                Helpers.SetClassifiedAs(level, laSet, laItem);

                Helpers.ProcessAltRefNo(record, laObj);


            }

            Sample(archiveByRefNo, 1000, true);
        }


        private static void Sample(
            Dictionary<string, LinkedArtObject> archiveByRefNo,
            int interval, bool writeToDisk)
        {
            List<string> pleaseDump = [
                "WGC/1/1/150",
                "WR/109"
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
