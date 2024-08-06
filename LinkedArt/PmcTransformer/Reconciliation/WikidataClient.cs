using System.Text.Json;

namespace PmcTransformer.Reconciliation
{
    public class WikidataClient
    {
        private readonly HttpClient httpClient;

        private static DateTime LastCalled = DateTime.Now;

        public WikidataClient(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        public async Task<IdentifierAndLabel?> GetName(string identifier)
        {
            await RateLimit();
            var url = $"https://www.wikidata.org/wiki/Special:EntityData/{identifier}?flavor=dump";
            var stream = await httpClient.GetStreamAsync(url) ;
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



        private static async Task RateLimit()
        {
            const int crawlDelay = 2000;
            var msSinceLastCall = (DateTime.Now - LastCalled).TotalMilliseconds;
            LastCalled = DateTime.Now;
            if (msSinceLastCall < crawlDelay)
            {
                var timeToWait = crawlDelay - (int)msSinceLastCall;
                Console.WriteLine($"Delaying Wikidata Client for {timeToWait} ms");
                await Task.Delay(timeToWait);
            }
        }
    }
}
