using Dapper;
using LinkedArtNet;
using Npgsql;
using PmcTransformer.Helpers;
using System.Text.Json;

namespace PmcTransformer.Reconciliation
{
    public class UlanClient
    {
        // uses local DB
        private static NpgsqlConnection conn = DbCon.Get();

        public static List<IdentifierAndLabel> GetIdentifiersAndLabels(string label, string type)
        {
            var ulans = conn.Query<IdentifierAndLabel>(
                "select identifier, label from ulan_labels where label=@label and type=@type",
                new { label, type })
                .ToList();
            return ulans;
        }

        public static Actor? GetFromIdentifier(string identifier)
        {
            var actorStr = conn.QuerySingleOrDefault<string>(
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
