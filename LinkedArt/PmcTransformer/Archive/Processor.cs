using LinkedArtNet;
using LinkedArtNet.Parsers;
using LinkedArtNet.Vocabulary;
using PmcTransformer.Reconciliation;
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

            var authorityDict = AuthorityParser.CreateArchiveAuthorityDict(xAuthorities);
            // can't do this as we have duplicate primary names
            // we'll just have to use the first
            // var creatorNameDict = authorityDict.Values.ToDictionary(GetPrimaryName);
            var creatorNameDict = new Dictionary<string, Actor>();
            foreach (var item in authorityDict)
            {
                var primaryName = GetPrimaryName(item.Value);
                if(creatorNameDict.ContainsKey(primaryName))
                {
                    Console.WriteLine(item.Key + " - Duplicate Authority PersonName: " + primaryName);
                }
                else
                {
                    creatorNameDict.Add(primaryName, item.Value);
                }                
            }

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

                // All archival things are members of this set
                laObj.MemberOf = [ PmcTransformer.Helpers.Locations.PMCArchiveSetRef ];
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
                        laObj.MemberOf.Add(parentRef);
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
                var dateField = record.ArcStrings("Date").SingleOrDefault();
                Helpers.ProcessDate(dateField, laObj);

                foreach (var creatorName in record.ArcStrings("CreatorName"))
                {
                    var creator = TryMatchCreator(creatorName, creatorNameDict);
                    if(creator != null)
                    {
                        laObj.CreatedBy = new Activity(Types.Creation)
                        {
                            CarriedOutBy = [creator.GetReferenceObject()]
                        };
                    }
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

                foreach (var creatorCode in record.ArcStrings("RelatedNameCode"))
                {
                    if (authorityDict.ContainsKey(creatorCode))
                    {
                        var authority = authorityDict[creatorCode];
                        laObj.About ??= [];
                        laObj.About.Add(authority.GetReferenceObject());
                    }
                    else
                    {
                        Console.WriteLine(creatorCode + " is NOT in Authority File");
                    }
                }
                var relatedNames = record.ArcStrings("RelatedName");
                var relatedNameRelationShips = record.ArcStrings("RelatedNameRelationship");
                var relatedNameDecription = record.ArcStrings("RelatedNameDecription");
                if(relatedNames.Any() || relatedNameRelationShips.Any() || relatedNameDecription.Any()) 
                {
                    Console.WriteLine("SHOULD NOT HAVE relatedXXX: " + refNo);
                }




                Writer.WriteToDisk(laObj);
            }

            // Now we want to reconcile the actors in authorityDict
            // to the authorities we already have from the library reconcilation.

            var conn = DbCon.Get();
            foreach(var actor in authorityDict.Values)
            {
                var simpleMatches = conn.FindByEquivalence(actor);
                foreach(var simpleMatch in simpleMatches)
                {
                    actor.Equivalent ??= [];
                    actor.Equivalent.Add(simpleMatch.GetReference()!);
                }
                Writer.WriteToDisk(actor);
            }
            // TODO - for now don't assert equivalence - come back and do that later, let's just get them out there

        }

        private static Actor? TryMatchCreator(string creatorName, Dictionary<string, Actor> creatorDict)
        {
            // Dumb exact match:
            if(creatorDict.ContainsKey(creatorName))
            {
                return creatorDict[creatorName];
            }
            if(CreatorDictEquivalents.ContainsKey(creatorName))
            {
                return creatorDict[CreatorDictEquivalents[creatorName]];
            }
            // ok so not an exact match... but is there a partial?
            Console.WriteLine("No Creator: " + creatorName);
            return null;
        }

        private static readonly Dictionary<string, string> CreatorDictEquivalents = new Dictionary<string, string>()
        {
            ["Paul Oppé"] = "Oppé; Adolf Paul (1878-1957)",
            // ["Wright, Christopher (1945-)"] = "", 
            ["Sharp, Dennis Charles (1933-2010) Architect and author"] = "Sharp; Dennis Charles (1933-2010)",
            ["(Derick) Humphrey Waterfield"] = "Waterfield; Derick Humphrey (1908-1971); Mr",
            ["Haldin, Daphne Louise (1899-1973)"] = "Haldin; Daphne Louise (1899\u00961973)",  // &ndash;
            ["Waterhouse; Sir Ellis Kirkham(1905-1985)"] = "Waterhouse; Ellis Kirkham (1905-1985)",
            // ["Simpson, Frank(Francis) Henry, (1911-2002)"] = "",
            // ["Sunderland; John Norman(1942-2018)"] = "",
            // ["John Anderson Stuart Ingamells"] = "", 
            // ["Hayes, John Trevor(1933-2005)"] = "",
            // ["Lionel Benedict Nicolson(1914-1978)"] = "",
            ["Surry, Nigel W(1936-"] = "Surry; Nigel W (1936-)",
            // ["Oliver Nicholas Millar"] = "",
            // ["Paul R. Joyce"] = "",
            ["Ford, Sir, Richard Brinsley (1908-1999) Knight"] = "Ford; Sir; Richard Brinsley (1908-1999)",
            ["Constable; William George(1887-1976)"] = "Constable; William George (1887-1976)",
            // ["William Roberts(1862-1940)"] = ""
        };

        private static string GetPrimaryName(Actor actor)
        {
            var primaryName = actor.IdentifiedBy!.Where(name => 
                name.ClassifiedAs != null 
                && name.ClassifiedAs.SingleOrDefault(ca => ca.Id == "http://vocab.getty.edu/aat/300404670") != null).Single();
            return ((Name)primaryName).Content!;
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
