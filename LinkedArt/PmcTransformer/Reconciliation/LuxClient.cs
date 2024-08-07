using LinkedArtNet;
using System;
using System.Text.Json;

namespace PmcTransformer.Reconciliation
{
    public class LuxClient
    {
        private readonly HttpClient httpClient;

        public LuxClient(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        public async Task<List<Place>> PlaceSearch(string term)
        {
            const string template = "https://lux.collections.yale.edu/api/search/place?q=%7B%22AND%22%3A%5B%7B%22name%22%3A%22{term}%22%2C%22_options%22%3A%5B%22unstemmed%22%5D%2C%22_complete%22%3Atrue%7D%5D%7D";
            var uri = template.Replace("{term}", Uri.EscapeDataString(term));
            var results = new List<Place>();
            try
            {
                var stream = await httpClient.GetStreamAsync(uri);
                using (JsonDocument jDoc = JsonDocument.Parse(stream))
                {
                    var orderedItems = jDoc.RootElement.GetProperty("orderedItems");
                    foreach (var item in orderedItems.EnumerateArray())
                    {
                        var itemStream = await httpClient.GetStreamAsync(item.GetProperty("id").GetString());
                        var place = JsonSerializer.Deserialize<Place>(itemStream); 
                        if (place != null) 
                        { 
                            results.Add(place); 
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LUX error for place '{term}'");
                Console.WriteLine(ex.ToString());
            }

            return results;
        }

        public async Task<List<LinkedArtObject>> ConceptSearch(string term)
        {
            const string template = "https://lux.collections.yale.edu/api/search/concept?q=%7B%22AND%22%3A%5B%7B%22name%22%3A%22{term}%22%2C%22_options%22%3A%5B%22unstemmed%22%5D%2C%22_complete%22%3Atrue%7D%5D%7D";
            var uri = template.Replace("{term}", Uri.EscapeDataString(term));
            var results = new List<LinkedArtObject>();
            try
            {
                var stream = await httpClient.GetStreamAsync(uri);
                using (JsonDocument jDoc = JsonDocument.Parse(stream))
                {
                    var orderedItems = jDoc.RootElement.GetProperty("orderedItems");
                    foreach (var item in orderedItems.EnumerateArray())
                    {
                        var itemStream = await httpClient.GetStreamAsync(item.GetProperty("id").GetString());
                        var concept = JsonSerializer.Deserialize<LinkedArtObject>(itemStream);
                        if (concept != null)
                        {
                            results.Add(concept);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LUX error for concept '{term}'");
                Console.WriteLine(ex.ToString());
            }

            return results;
        }

        public async Task<List<Actor>> ActorsWhoCreatedWorks(string actorName, string workName)
        {
            // no rate limit but keep on single thread
            const string template = "https://lux.collections.yale.edu/api/search/agent?q=%7B%22AND%22%3A%5B%7B%22name%22%3A%22{actor}%22%7D%2C%7B%22created%22%3A%7B%22name%22%3A%22{work}%22%7D%7D%5D%7D";
            var t1 = template.Replace("{actor}", Uri.EscapeDataString(actorName));
            var uri = t1.Replace("{work}", 
                Uri.EscapeDataString(workName)
                    .Replace("%22", "\\%22")
                    .Replace("%3F", "\\\\%3F"));
            var results = new List<Actor>();
            try
            {
                var stream = await httpClient.GetStreamAsync(uri);
                using (JsonDocument jDoc = JsonDocument.Parse(stream))
                {
                    var orderedItems = jDoc.RootElement.GetProperty("orderedItems");
                    foreach (var item in orderedItems.EnumerateArray())
                    {
                        var itemType = item.GetProperty("type").GetString();
                        var itemStream = await httpClient.GetStreamAsync(item.GetProperty("id").GetString());
                        if (itemType == "Group")
                        {
                            var group = JsonSerializer.Deserialize<Group>(itemStream);
                            if (group != null) { results.Add(group); }
                        }
                        if (itemType == "Person")
                        {
                            var person = JsonSerializer.Deserialize<Person>(itemStream);
                            if (person != null) { results.Add(person); }
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"LUX error for actor '{actorName}' created work '{workName}'");
                Console.WriteLine(ex.ToString());
            }
            
            return results;
        }
    }
}
