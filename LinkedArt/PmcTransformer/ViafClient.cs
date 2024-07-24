using System.Text.Json;

namespace PmcTransformer
{
    public class ViafClient
    {
        private static HttpClient httpClient;
        private static JsonSerializerOptions prettyJson;

        static ViafClient()
        {
            httpClient = new HttpClient(new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(15)
            });
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Paul Mellon Centre Linked Art Client");
            prettyJson = new JsonSerializerOptions { WriteIndented = true };
        }

        public static Authority? SearchBestMatch(string prefix, string term)
        {
            // prefix:  local.corporateNames%20all%20%22Courtauld%20Institute%20of%20Art%2C%20University%20of%20London%22&recordSchema=BriefVIAF
            Thread.Sleep(2000);
            var sanitised = term.Replace("[", "(").Replace("]", ")");
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
                if(searchResp.GetProperty("numberOfRecords").GetString() != "0") // a string not an int
                {
                    string? viafId = null;
                    foreach (var record in searchResp.GetProperty("records").EnumerateArray())
                    {
                        var recordData = record.GetProperty("record").GetProperty("recordData");
                        viafId = recordData.GetProperty("viafID").GetProperty("#text").GetString();
                        var headingsData = recordData.GetProperty("mainHeadings").GetProperty("data");
                        if(headingsData.ValueKind == JsonValueKind.Array)
                        {
                            foreach(var heading in headingsData.EnumerateArray())
                            {
                                AddToHeadingsDict(mainHeadings, viafIDs, viafId, heading);
                            }
                        }
                        else
                        {
                            AddToHeadingsDict(mainHeadings, viafIDs, viafId, headingsData);
                        }
                    }

                    // Now we have a load of strings
                    var best = FuzzySharp.Process.ExtractOne(term, mainHeadings.Keys);

                    Console.WriteLine("Found from VIAF for " + term);
                    foreach(var key in mainHeadings.Keys)
                    {
                        Console.WriteLine("   " + key);
                    }
                    Console.WriteLine("best: " + best.Value);
                    Console.WriteLine("score: " + best.Score);
                    var authority = new Authority() 
                    { 
                        Label = best.Value,
                        Viaf = viafIDs[best.Value]
                    };
                    var sids = mainHeadings[best.Value].GetProperty("sources").GetProperty("sid");
                    var sidStrings = new Dictionary<string, string>();
                    if(sids.ValueKind == JsonValueKind.Array)
                    {
                        sidStrings = sids.EnumerateArray().Select(sid => sid.GetString())
                            .ToDictionary(s => s!.Split('|')[0], s => s!.Split('|')[1]);
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
                    if(sidStrings.ContainsKey("WKP"))
                    {
                        authority.WikiData = sidStrings["WKP"];
                    }
                    // anything else to pull from VIAF?
                    return authority;
                }
            }
            return null;
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
