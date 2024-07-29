using System.Text.Json;

namespace PmcTransformer.Reconciliation
{
    public class WikidataClient
    {
        private static HttpClient httpClient;

        static WikidataClient()
        {
            httpClient = HttpClients.GetStandardClient();
        }

        public static IdentifierAndLabel? GetName(string identifier)
        {
            Thread.Sleep(2000);
            var url = $"https://www.wikidata.org/wiki/Special:EntityData/{identifier}?flavor=dump";
            var resp = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, url));
            resp.EnsureSuccessStatusCode();
            var stream = resp.Content.ReadAsStream();
            using (JsonDocument jDoc = JsonDocument.Parse(stream))
            {
                var entities = jDoc.RootElement.GetProperty("entities");
                var entity = entities.GetProperty(identifier);
                var labels = entity.GetProperty("labels");
                if(entity.TryGetProperty("en", out JsonElement enLabel))
                {
                    return new IdentifierAndLabel 
                    { 
                        Identifier = identifier, 
                        Label = enLabel.GetProperty("value").GetString()! 
                    };
                }
                // no en label...
                return new IdentifierAndLabel { 
                    Identifier = identifier, 
                    Label = labels.EnumerateObject().First().Value.GetProperty("value").GetString()! 
                };
            }
        }
    }
}
