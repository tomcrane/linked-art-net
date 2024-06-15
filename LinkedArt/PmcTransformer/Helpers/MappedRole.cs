
using LinkedArtNet;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PmcTransformer.Helpers
{
    public class MappedRole
    {

        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("type")]
        public required string Type { get; set; }

        [JsonPropertyName("_label")]
        public required string Label { get; set; }

        [JsonPropertyName("_activity")]
        public required string Activity { get; set; }

        [JsonPropertyName("_note")]
        public string? Note { get; set; }


        [JsonIgnore]
        public static Dictionary<string, int> Roles = [];

        [JsonIgnore]
        private static Dictionary<string, MappedRole>? roleActivityMap = null;

        public static Activity GetActivityWithPart(string role)
        {
            // for analytics
            if (!Roles.ContainsKey(role))
            {
                Roles[role] = 0;
            }
            Roles[role] = Roles[role] + 1;

            const string source = "https://raw.githubusercontent.com/digirati-co-uk/pmc-lux/main/mappings/library/roles.json";
            if (roleActivityMap == null)
            {
                var resp = new HttpClient().Send(new HttpRequestMessage(HttpMethod.Get, source));
                var stream = resp.Content.ReadAsStream();
                roleActivityMap = JsonSerializer.Deserialize<Dictionary<string, MappedRole>>(stream);
            }

            // need to handle missing role for new items
            // shouldn't stop running, should be flagged to add to map
            var mappedRole = roleActivityMap![role];

            var activity = new Activity(Types.Creation)
            {
                Part = []
            };
            activity.Part.Add(new Activity(mappedRole.Activity)
            {
                ClassifiedAs = [
                    new LinkedArtObject(Types.Type).WithId(mappedRole.Id).WithLabel(mappedRole.Label)
                ]
            });
            return activity;
        }
    }


}
