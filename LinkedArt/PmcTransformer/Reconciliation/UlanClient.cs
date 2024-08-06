using Dapper;
using LinkedArtNet;
using Npgsql;
using PmcTransformer.Helpers;
using System.Text.Json;

namespace PmcTransformer.Reconciliation
{
    public class UlanClient
    {
        private readonly NpgsqlConnection conn = DbCon.Get();

        public UlanClient() 
        { 
        }

        public async Task<List<IdentifierAndLabel>> GetIdentifiersAndLabels(string label, string type)
        {
            var ulans = await conn.QueryAsync<IdentifierAndLabel>(
                "select identifier, label from ulan_labels where label=@label and type=@type",
                new { label, type });
            return ulans.ToList();
        }
        public async Task<List<IdentifierAndLabel>> GetIdentifiersAndLabelsLevenshtein(string label, string type, int limit)
        {
            if(label.Length > 255)
            {
                Console.WriteLine("Cannot match on strings > 255 characters (postgres levenshtein limit)");
                return [];
            }
            var matchesQ = await conn.QueryAsync<IdentifierAndLabel>(
                "select identifier, label, levenshtein(label, @label) as score " +
                "from ulan_labels where type=@type " +
                "order by levenshtein(label, @label) asc limit @limit",
                new { label, type, limit });
            var matches = matchesQ.ToList();
            foreach (var match in matches)
            {
                // convert levenshtein distances to percent (factors in length of string)
                match.Score = 100 - Convert.ToInt32(100 * (match.Score / (decimal)label.Length));
            }
            return matches;
        }

        public async Task<Actor?> GetFromIdentifier(string identifier)
        {
            var actorStr = await conn.QuerySingleOrDefaultAsync<string>(
                "select data from ulan_data_cache where identifier=@identifier",
                new { identifier });
            if(actorStr.HasText())
            {
                using (JsonDocument jDoc = JsonDocument.Parse(actorStr))
                {
                    var type = jDoc.RootElement.GetProperty("type").GetString();
                    if (type == "Group")
                    {
                        return JsonSerializer.Deserialize<Group>(jDoc);
                    }
                    if (type == "Person")
                    {
                        return JsonSerializer.Deserialize<Person>(jDoc);
                    }
                }
            }
            return null;
        }
    }
}
