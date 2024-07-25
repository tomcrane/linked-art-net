using System;
using System.IO;
using System.Text.Json;

namespace PmcTransformer.Helpers
{
    public class KnownAuthorities
    {
        const string GroupSource = "https://raw.githubusercontent.com/digirati-co-uk/pmc-lux/main/authority-strings/groups.json";
        private static Dictionary<string, Authority>? Groups = null;
        private static Dictionary<string, string>? RawStringLookUps = null;

        static KnownAuthorities()
        {
            var httpClient = new HttpClient();

            var resp1 = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, GroupSource));
            var stream1 = resp1.Content.ReadAsStream();

            using (JsonDocument jDoc = JsonDocument.Parse(stream1))
            {
                RawStringLookUps = JsonSerializer.Deserialize<Dictionary<string, string>>(jDoc.RootElement.GetProperty("local_string_map"));
                var authorities = JsonSerializer.Deserialize<List<Authority>>(jDoc.RootElement.GetProperty("authorities"));
                Groups = authorities!.ToDictionary(a => a.Identifier!);
            }


            // and people...
        }

        public static Authority? GetGroup(string sourceString)
        {
            if (RawStringLookUps!.TryGetValue(sourceString, out string? value))
            {
                return Groups![value];
            }
            return null;
        }
    }
}
