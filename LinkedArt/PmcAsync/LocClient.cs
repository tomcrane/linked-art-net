using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace PmcAsync
{
    public class LocClient
    {
        private readonly HttpClient httpClient;
        private readonly JsonSerializerOptions prettyJson = new JsonSerializerOptions { WriteIndented = true };

        private const string locName = "http://id.loc.gov/authorities/names/";
        private const string locNameS = "https://id.loc.gov/authorities/names/";

        private static DateTime LastCalled = DateTime.Now;

        public LocClient(HttpClient httpClient)
        {
            this.httpClient = httpClient;
            this.httpClient.BaseAddress = new Uri(locNameS);
        }

        public async IdentifierAndLabel? GetName(string identifier)
        {
            var msSinceLastCall = (DateTime.Now - LastCalled).TotalMilliseconds;
            LastCalled = DateTime.Now;
            if (msSinceLastCall < 3000) 
            {
                await Task.Delay((int)(3000 - msSinceLastCall));
            }
            var url = $"{locNameS}{identifier}.skos.json";
            var resp = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, url));
            resp.EnsureSuccessStatusCode();
            var stream = resp.Content.ReadAsStream();
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

        public static List<IdentifierAndLabel> SuggestName(string name)
        {
            // https://id.loc.gov/robots.txt asks for a Crawl-delay of 3
            Thread.Sleep(3000);
            const string url = "https://id.loc.gov/authorities/names/suggest/?q=";
            var req = new HttpRequestMessage(HttpMethod.Get, url + HttpUtility.UrlEncode(name));
            var resp = httpClient.Send(req);
            var results = new List<IdentifierAndLabel>();
            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine("ERROR talking to LOC: " + resp.StatusCode + " " + req.RequestUri);
                return results;
            }
            resp.EnsureSuccessStatusCode();
            var stream = resp.Content.ReadAsStream();
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
                            Identifier = jDoc.RootElement[3][0].GetString()!.Replace(locName, ""),
                            Label = jDoc.RootElement[1][0].GetString()!
                        });
                    }
                }
            }
            return results;
        }
    }
}
