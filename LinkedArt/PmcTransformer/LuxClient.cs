using LinkedArtNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PmcTransformer
{
    public class LuxClient
    {
        private static HttpClient httpClient;
        private static JsonSerializerOptions prettyJson;

        static LuxClient()
        {
            httpClient = new HttpClient(new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(15)
            });
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Paul Mellon Centre Linked Art Client");
            prettyJson = new JsonSerializerOptions { WriteIndented = true };
        }

        public static List<Actor> ActorsWhoCreatedWorks(string actorName, string workName)
        {
            Thread.Sleep(500);
            const string template = "https://lux.collections.yale.edu/api/search/agent?q=%7B%22AND%22%3A%5B%7B%22name%22%3A%22{actor}%22%7D%2C%7B%22created%22%3A%7B%22name%22%3A%22{work}%22%7D%7D%5D%7D";
            var t1 = template.Replace("{actor}", Uri.EscapeDataString(actorName));
            var uri = t1.Replace("{work}", Uri.EscapeDataString(workName));
            var req = new HttpRequestMessage(HttpMethod.Get, uri);
            var resp = httpClient.Send(req);
            resp.EnsureSuccessStatusCode();
            var stream = resp.Content.ReadAsStream();
            var results = new List<Actor>();
            using (JsonDocument jDoc = JsonDocument.Parse(stream))
            {
                var orderedItems = jDoc.RootElement.GetProperty("orderedItems");
                foreach (var item in orderedItems.EnumerateArray())
                {
                    var itemType = item.GetProperty("type").GetString();
                    var itemReq = new HttpRequestMessage(HttpMethod.Get, item.GetProperty("id").GetString());
                    var itemResp = httpClient.Send(itemReq);
                    itemResp.EnsureSuccessStatusCode();
                    var itemStream = itemResp.Content.ReadAsStream();

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
            return results;
        }
    }
}
