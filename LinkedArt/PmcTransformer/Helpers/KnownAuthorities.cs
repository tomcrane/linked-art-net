using System.Text.Json;

namespace PmcTransformer.Helpers
{
    public class KnownAuthorities
    {
        const string GroupSource = "https://raw.githubusercontent.com/digirati-co-uk/pmc-lux/main/authority-strings/groups.json";
        const string PeopleSource = "https://raw.githubusercontent.com/digirati-co-uk/pmc-lux/main/authority-strings/people.json";
        private static Dictionary<string, Authority>? Groups = null;
        private static Dictionary<string, Authority>? People = null;
        private static Dictionary<string, string>? RawStringLookUpsGroups = null;
        private static Dictionary<string, string>? RawStringLookUpsPeople = null;

        static KnownAuthorities()
        {
            var httpClient = new HttpClient();

            var resp1 = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, GroupSource));
            var stream1 = resp1.Content.ReadAsStream();

            using (JsonDocument jDoc = JsonDocument.Parse(stream1))
            {
                RawStringLookUpsGroups = JsonSerializer.Deserialize<Dictionary<string, string>>(jDoc.RootElement.GetProperty("local_string_map"));
                var authorities = JsonSerializer.Deserialize<List<Authority>>(jDoc.RootElement.GetProperty("authorities"));
                Groups = authorities!.ToDictionary(a => a.Identifier!);
            }

            var resp2 = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, PeopleSource));
            var stream2 = resp2.Content.ReadAsStream();

            using (JsonDocument jDoc = JsonDocument.Parse(stream2))
            {
                RawStringLookUpsPeople = JsonSerializer.Deserialize<Dictionary<string, string>>(jDoc.RootElement.GetProperty("local_string_map"));
                var authorities = JsonSerializer.Deserialize<List<Authority>>(jDoc.RootElement.GetProperty("authorities"));
                People = authorities!.ToDictionary(a => a.Identifier!);
            }

        }

        public static Authority? GetGroup(string sourceString)
        {
            if (RawStringLookUpsGroups!.TryGetValue(sourceString, out string? value))
            {
                return Groups![value];
            }
            return null;
        }

        public static Authority? GetPerson(string sourceString)
        {
            if (RawStringLookUpsPeople!.TryGetValue(sourceString, out string? value))
            {
                return People![value];
            }
            return null;
        }
    }
}
