using LinkedArtNet;
using LinkedArtNet.Parsers;
using LinkedArtNet.Vocabulary;
using System.Text.Json;

namespace PmcTransformer.Leeds
{
    public class Processor
    {
        public static void ProcessExamples()
        {
            const string rawFolder = "C:\\pmc\\leeds\\raw\\";

            var jHill8501 = JsonDocument.Parse(File.ReadAllText(rawFolder + "8501.json"));
            var jRoth114260 = JsonDocument.Parse(File.ReadAllText(rawFolder + "114260.json"));
            var jRoth115535 = JsonDocument.Parse(File.ReadAllText(rawFolder + "115535.json"));
            var jBauman706606 = JsonDocument.Parse(File.ReadAllText(rawFolder + "706606.json"));

            List<JsonDocument> jDocs = [jHill8501, jRoth114260, jRoth115535, jBauman706606];

            var uriBase = "https://library.leeds.ac.uk/archive/";
            var leedsSet = new LinkedArtObject(Types.Set)
                .WithId(uriBase + "_all")
                .WithLabel("University Archive Collection");

            foreach (var jDoc in jDocs)
            {
                var record = jDoc.RootElement.EnumerateArray().First();

                var id = record.GetProperty("id").GetInt32();
                var refNo = record.GetProperty("EADUnitID").GetString()!;
                var level = record.GetProperty("EADLevelAttribute").GetString();
                var title = record.GetProperty("EADUnitTitle").GetString();

                bool isItem = level == "Item";
                LinkedArtObject? laSet = isItem ? null : new LinkedArtObject(Types.Set);
                HumanMadeObject? laItem = isItem ? new HumanMadeObject() : null;  // DISCUSS!!!! DigitalObject too
                // DigitalObject that carries a LinguisticObject - but in that case should the HMO carry one too?
                // Is that pointless... the aboutness is the Linguistic not the physical or digital
                // raise on LA slack
                LinkedArtObject laObj = (laSet ?? laItem)!;
                LinkedArtObject? parentRef = null;

                laObj.MemberOf = [leedsSet];

                var lastIDPart = refNo.Split(' ')[^1];
                (string sortRefNo, string? parentRefNo) = Archive.Helpers.GetRefNoVariants(lastIDPart);

                if (record.TryGetProperty("AssParentObjectRef", out JsonElement parent))
                {
                    parentRef = GetSummaryReference(uriBase, parent);
                    laObj.MemberOf.Add(parentRef);
                }

                laObj.WithContext().WithId(uriBase + id);
                laObj.IdentifiedBy = [
                    new Identifier(refNo).WithClassifiedAs(Getty.RecordIdentifiers),
                    new Name($"{refNo} - {title}").AsPrimaryName(),
                    Identifier.SortValue(sortRefNo, parentRef)
                ];

                Archive.Helpers.SetClassifiedAs(level, laSet, laItem);

                var dateField = record.GetProperty("EADUnitDate").GetString().TrimOuterBrackets();
                Archive.Helpers.ProcessDate(dateField, laObj);

                // Creation - TODO
                // laObj.CreatedBy = new Activity(Types.Creation)
                // {
                //     CarriedOutBy = [creator.GetReferenceObject()]
                // };


                // statements/descriptions
                if(record.TryGetProperty("EADExtent_tab", out JsonElement jExtent))
                {
                    var extentList = jExtent.EnumerateArray().Select(x => x.GetString());
                    foreach (var extent in extentList)
                    {
                        Archive.Helpers.SimpleStatement(extent, laObj, Getty.DimensionStatement);
                    }
                }
                
                // ?? Archive.Helpers.SimpleStatement(record, laObj, "AdminHistory", Getty.AdministrativeHistory);
                
                if(record.TryGetProperty("EADCustodialHistory", out JsonElement jCustodial))
                {
                    Archive.Helpers.SimpleStatement(jCustodial.GetString(), laObj, Getty.ProvenanceStatement);
                }

                if (record.TryGetProperty("EADScopeAndContent", out JsonElement jDesc))
                {
                    Archive.Helpers.SimpleStatement(jDesc.GetString(), laObj, Getty.Description);
                }

                // ?? Archive.Helpers.SimpleStatement(record, laObj, "Accruals", Getty.Accruals);

                if (record.TryGetProperty("EADArrangement", out JsonElement jArr))
                {
                    Archive.Helpers.SimpleStatement(jArr.GetString(), laObj, Getty.ArrangementDescription);
                }

                // ?? easy Archive.Helpers.SimpleStatement(record, laObj, "AccessConditions", Getty.AccessStatement);

                if (record.TryGetProperty("EADRelatedMaterial", out JsonElement jRelStr))
                {
                    Archive.Helpers.SimpleStatement(jRelStr.GetString(), laObj, Getty.RelatedMaterial);
                }

                //if(record.TryGetProperty("AssRelatedObjectsRef_tab", out JsonElement jRelObjs))
                //{
                //    // NOT LIKE THIS!
                //    laObj.Part = [];
                //    foreach(var jObj in jRelObjs.EnumerateArray())
                //    {
                //        var set = GetSummaryReference(uriBase, jObj);
                //        laObj.Part.Add(set);    
                //    }
                //}

                List<string> subjectSources = ["CreSubjectClassification_tab", "EADSubject_tab", "LeeLibrarySubjects_tab"];
                foreach (var subjectSource in subjectSources)
                {
                    if(record.TryGetProperty(subjectSource, out JsonElement subjects))
                    {
                        foreach (var subject in subjects.EnumerateArray())
                        {
                            var thing = new LinkedArtObject(Types.Type)
                                .WithId(uriBase + "subjects/" + IdMinter.Generate())
                                .WithLabel(subject.GetString());
                            laObj.About ??= [];
                            laObj.About.Add(thing);
                        }
                    }
                }


                if (record.TryGetProperty("EADBiographyOrHistory", out JsonElement jBio))
                {
                    // may not be biographical though...
                    Archive.Helpers.SimpleStatement(jBio.GetString(), laObj, Getty.BiographyStatement);
                }

                // Archive.Helpers.SimpleStatement(record, laObj, "PublnNote", Getty.GeneralNote);




                WriteToDisk(rawFolder, id, laObj);
            }

        }

        private static LinkedArtObject GetSummaryReference(string uriBase, JsonElement parent)
        {
            // This will not always be a Set but ok for demo
            return new LinkedArtObject(Types.Set)
                                    .WithId(uriBase + parent.GetProperty("irn").GetInt32())
                                    .WithLabel(parent.GetProperty("SummaryData").GetString());
        }

        private static void WriteToDisk(string rawFolder, int id, LinkedArtObject laObj)
        {
            var json = JsonSerializer.Serialize(laObj, options);
            var fi = new FileInfo(Path.Combine(rawFolder, "output", $"{id}.json"));
            Directory.CreateDirectory(fi.DirectoryName);
            File.WriteAllText(fi.FullName, json);
        }

        private static readonly JsonSerializerOptions options = new() { WriteIndented = true, };
    }
}
