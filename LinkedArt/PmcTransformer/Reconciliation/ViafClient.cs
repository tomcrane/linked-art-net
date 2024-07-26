using System.Text.Json;
using System.Web;

namespace PmcTransformer.Reconciliation
{
    public class ViafClient
    {
        private static HttpClient httpClient;

        static ViafClient()
        {
            httpClient = HttpClients.GetStandardClient();
        }

        public static IdentifierAndLabel? GetName(string identifier)
        {
            try
            {
                var url = $"https://viaf.org/viaf/{identifier}/";
                var resp = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, url));
                resp.EnsureSuccessStatusCode();
                var stream = resp.Content.ReadAsStream();
                using (JsonDocument jDoc = JsonDocument.Parse(stream))
                {
                    var headingsData = GetMainHeadingsData(jDoc.RootElement);
                    // just take the first?
                    if (headingsData.Count != 0)
                    {
                        return new IdentifierAndLabel
                        {
                            Identifier = identifier,
                            Label = headingsData[0].GetProperty("text").GetString()!
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                // possibly a redirect - should follow - see Rijkmuseum
            }
            return null;
        }

        public static List<Authority> SearchBestMatch(string prefix, string term)
        {
            var sanitised = HttpUtility.UrlEncode(term.Replace("[", "(").Replace("]", ")"));
            string uri = $"https://viaf.org/viaf/search?query={prefix}%22{sanitised}%22&recordSchema=BriefVIAF";
            var req = new HttpRequestMessage(HttpMethod.Get, uri);
            var resp = httpClient.Send(req);
            resp.EnsureSuccessStatusCode();
            var stream = resp.Content.ReadAsStream();
            using (JsonDocument jDoc = JsonDocument.Parse(stream))
            {
                var mainHeadings = new Dictionary<string, JsonElement>();
                var viafIDs = new Dictionary<string, string>();
                var searchResp = jDoc.RootElement.GetProperty("searchRetrieveResponse");
                if (searchResp.GetProperty("numberOfRecords").GetString() != "0") // a string not an int
                {
                    string? viafId = null;
                    foreach (var record in searchResp.GetProperty("records").EnumerateArray())
                    {
                        var recordData = record.GetProperty("record").GetProperty("recordData");
                        viafId = recordData.GetProperty("viafID").GetProperty("#text").GetString();
                        var headingsData = GetMainHeadingsData(recordData); 
                        foreach (var heading in headingsData)
                        {
                            AddToHeadingsDict(mainHeadings, viafIDs, viafId, heading);
                        }
                    }

                    // Now we have a load of strings
                    var scored = FuzzySharp.Process.ExtractSorted(term, mainHeadings.Keys);
                    Console.WriteLine("Found from VIAF for " + term);
                    foreach (var match in scored)
                    {
                        Console.WriteLine($"{match.Score} - {match.Value}");
                    }
                    var authorities = scored.Select(s => new Authority 
                                                        { 
                                                            Score = s.Score,
                                                            Label = s.Value, 
                                                            Viaf = viafIDs[s.Value] 
                                                        }
                                                    ).OrderByDescending(a => a.Score).ToList();
                    foreach(var authority in authorities)
                    {
                        var sids = mainHeadings[authority.Label!].GetProperty("sources").GetProperty("sid");
                        var sidStrings = new Dictionary<string, string>();
                        if (sids.ValueKind == JsonValueKind.Array)
                        {
                            var sidStringList = sids.EnumerateArray().Select(sid => sid.GetString()).OfType<string>();
                            //    .ToDictionary(s => s!.Split('|')[0], s => s!.Split('|')[1]); // can't do this because duplicate keys
                            // see https://viaf.org/viaf/search?query=local.corporateNames%20all%20%22W/S%20Fine%20Art%20Ltd%22&recordSchema=BriefVIAF&httpAccept=application/json for an example
                            foreach (string s in sidStringList)
                            {
                                var key = s.Split('|')[0];
                                if (!sidStrings.ContainsKey(key))
                                {
                                    sidStrings[key] = s.Split('|')[1];
                                }
                            }
                        }
                        else
                        {
                            sidStrings = new Dictionary<string, string> {
                            { sids.GetString()!.Split('|')[0], sids.GetString()!.Split('|')[1] }
                        };
                        }
                        if (sidStrings.ContainsKey("LC"))
                        {
                            authority.Loc = sidStrings["LC"].Replace(" ", "");
                        }
                        if (sidStrings.ContainsKey("WKP"))
                        {
                            authority.Wikidata = sidStrings["WKP"];
                        }
                    }
                    // anything else to pull from VIAF?
                    return authorities;
                }
            }
            return new List<Authority>();
        }

        private static List<JsonElement> GetMainHeadingsData(JsonElement recordData)
        {
            var result = new List<JsonElement>();   
            var headingsData = recordData.GetProperty("mainHeadings").GetProperty("data");
            if (headingsData.ValueKind == JsonValueKind.Array)
            {
                foreach (var heading in headingsData.EnumerateArray())
                {
                    result.Add(heading);
                }
            }
            else
            {
                result.Add(headingsData);
            }
            return result;
        }

        private static void AddToHeadingsDict(Dictionary<string, JsonElement> mainHeadings, Dictionary<string, string> viafIDs, string? viafId, JsonElement heading)
        {
            var text = heading.GetProperty("text").GetString()!;
            if (mainHeadings.ContainsKey(text))
            {
                Console.WriteLine("duplicate heading: " + text);
                // prefer the one that has an LC source
                var sources = heading.GetProperty("sources").GetProperty("s");
                if (sources.ValueKind == JsonValueKind.Array)
                {
                    if (sources.EnumerateArray().Any(source => source.GetString() == "LC"))
                    {
                        mainHeadings[text] = heading;
                        viafIDs[text] = viafId!;
                    }
                }
                else
                {
                    if (sources.GetString() == "LC")
                    {
                        mainHeadings[text] = heading;
                        viafIDs[text] = viafId!;
                    }
                }
            }
            else
            {
                mainHeadings[text] = heading;
                viafIDs[text] = viafId!;
            }
        }
    }
}
