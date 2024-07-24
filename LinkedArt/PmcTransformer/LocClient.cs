using System.Text.Json;

namespace PmcTransformer
{
    public class LocClient
    {
        private static HttpClient httpClient;
        private static JsonSerializerOptions prettyJson;

        static LocClient()
        {
            httpClient = new HttpClient(new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(15)
            });
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Paul Mellon Centre Linked Art Client");
            prettyJson = new JsonSerializerOptions {  WriteIndented = true };
        }

        public static List<IdentifierAndLabel> SuggestName(string name)
        {
            // https://id.loc.gov/robots.txt asks for a Crawl-delay of 3
            Thread.Sleep(3000);
            const string url = "https://id.loc.gov/authorities/names/suggest/?q=";
            var uri = new Uri(url + name);
            var req = new HttpRequestMessage(HttpMethod.Get, uri);
            var resp = httpClient.Send(req);
            resp.EnsureSuccessStatusCode();
            var stream = resp.Content.ReadAsStream();
            var results = new List<IdentifierAndLabel>();
            using (JsonDocument jDoc = JsonDocument.Parse(stream))
            {
                Console.WriteLine(JsonSerializer.Serialize(jDoc, prettyJson));
                if(jDoc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    // This assumes 1 result per match
                    for(int i=0; i< jDoc.RootElement[3].GetArrayLength(); i++)
                    {
                        results.Add(new IdentifierAndLabel()
                        {
                            Identifier = jDoc.RootElement[3][0].GetString()!
                                .Replace("http://id.loc.gov/authorities/names/", ""),
                            Label = jDoc.RootElement[1][0].GetString()!
                        });
                    }
                }
            }
            return results;
        }
    }
}
