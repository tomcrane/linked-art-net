using System.Text.Json;
using System.Web;

namespace PmcTransformer.Reconciliation
{
    public class LocClient
    {
        private readonly HttpClient httpClient;
        private readonly JsonSerializerOptions prettyJson = new JsonSerializerOptions { WriteIndented = true };

        private const string locName = "http://id.loc.gov/authorities/";
        private const string locNameS = "https://id.loc.gov/authorities/";

        private static DateTime LastCalled = DateTime.Now;

        public LocClient(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        public async Task<IdentifierAndLabel?> GetName(string category, string identifier)
        {
            await RateLimit();
            var url = $"{locNameS}{category}/{identifier}.skos.json";
            var stream = await httpClient.GetStreamAsync(url);
            using (JsonDocument jDoc = JsonDocument.Parse(stream))
            {
                foreach (var item in jDoc.RootElement.EnumerateArray())
                {
                    if (item.TryGetProperty("http://www.w3.org/2004/02/skos/core#prefLabel", out JsonElement prefLabelElement))
                    {
                        var label = prefLabelElement.EnumerateArray().First().GetProperty("@value").GetString();
                        return new IdentifierAndLabel { Identifier = identifier, Label = label! };
                    }
                }
            }
            return null;
        }

        private static async Task RateLimit()
        {
            const int crawlDelay = 3000;
            // https://id.loc.gov/robots.txt asks for a Crawl-delay of 3
            var msSinceLastCall = (DateTime.Now - LastCalled).TotalMilliseconds;
            LastCalled = DateTime.Now;
            if (msSinceLastCall < crawlDelay)
            {
                var timeToWait = crawlDelay - (int)msSinceLastCall;
                Console.WriteLine($"Delaying LOC Client for {timeToWait} ms");
                await Task.Delay(timeToWait);
            }
        }

        public async Task<List<IdentifierAndLabel>> SuggestName(string name)
        {
            var suggestions = await Suggest("names", name);
            return suggestions;
        }

        public async Task<List<IdentifierAndLabel>> SuggestHeading(string name)
        {
            var suggestions = await Suggest("subjects", name);
            return suggestions;
        }

        public async Task<List<IdentifierAndLabel>> SuggestPlace(string name)
        {
            // Need to see if this is good enough or whether we need to splelunk the results to 
            // narrow down to a place.
            var suggestions = await Suggest("names", name);
            return suggestions;
        }


        public async Task<List<IdentifierAndLabel>> Suggest(string category, string name)
        {            
            await RateLimit();
            string url = $"{locNameS}{category}/suggest/?q=";
            var results = new List<IdentifierAndLabel>();
            var reqUrl = url + HttpUtility.UrlEncode(name);
            try
            {
                var stream = await httpClient.GetStreamAsync(reqUrl);
                using (JsonDocument jDoc = JsonDocument.Parse(stream))
                {
                    Console.WriteLine(JsonSerializer.Serialize(jDoc, prettyJson));
                    if (jDoc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        // This assumes 1 result per match
                        for (int i = 0; i < jDoc.RootElement[3].GetArrayLength(); i++)
                        {
                            results.Add(new IdentifierAndLabel()
                            {
                                Identifier = jDoc.RootElement[3][0].GetString()!.Split('/')[^1],
                                Label = jDoc.RootElement[1][0].GetString()!
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR talking to LOC: " + ex.Message + " " + reqUrl);
                return results;
            }
            return results;
        }
    }
}
