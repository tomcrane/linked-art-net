using Dapper;
using Npgsql;
using PmcTransformer.Helpers;

namespace PmcTransformer
{
    public static class DbCon
    {
        public static NpgsqlConnection Get()
        {
            var dbpwd = Environment.GetEnvironmentVariable("pmc-db-pwd");
            return new NpgsqlConnection(
                connectionString: $"Server=localhost;Port=5432;User Id=postgres;Password={dbpwd};Database=pmc-linked-art;");
        }

        public static string? GetAuthorityIdentifier(this NpgsqlConnection conn,
            string source, string s, bool createEntryForSource = false)
        {
            var ssa = conn.QueryFirstOrDefault<SourceStringAndAuthority>(
                "select source, string, authority from source_string_map " +
                "where source=@source and string=@s", new { source, s });
            if(ssa == null && createEntryForSource)
            {
                conn.Execute($"insert into source_string_map (source, string, authority) " +
                              "values (@source, @s, null)", new { source, s });
            }
            return ssa?.Authority;
        }


        public static void UpsertAuthority(this NpgsqlConnection conn, 
            string dataSource, 
            string sourceString, 
            string provider,
            string type,
            IdentifierAndLabel identifierAndLabel)
        {
            var authority = conn.QuerySingleOrDefault<Authority>(
                $"select * from authorities where {provider}=@Identifier", new { identifierAndLabel.Identifier });
            if (authority == null)
            {
                var localIdentifier = IdMinter.Generate();
                conn.Execute($"insert into authorities (identifier, type, {provider}, label) " +
                             "values (@LocalIdentifier, @type, @Identifier, @Label)",
                             new { 
                                 LocalIdentifier = localIdentifier,
                                 type,
                                 identifierAndLabel.Identifier, 
                                 identifierAndLabel.Label
                             });
                authority = conn.QuerySingleOrDefault<Authority>(
                    $"select * from authorities where {provider}=@Identifier", new { identifierAndLabel.Identifier });
            }
            // authority is not null here
            conn.Execute("update source_string_map set authority=@Identifier " +
                         "where source=@dataSource and string=@sourceString",
                        new { authority!.Identifier, dataSource, sourceString });
        }

        public static void UpsertAuthority(this NpgsqlConnection conn,
            string dataSource,
            string sourceString,
            string type,
            Authority candidateAuthority)
        {
            string sql = "select * from authorities where ";
            if (candidateAuthority.Ulan.HasText())
            {
                sql += " ulan=@Ulan or ";
            }
            if (candidateAuthority.Aat.HasText())
            {
                sql += " aat=@Aat or ";
            }
            if (candidateAuthority.Lux.HasText())
            {
                sql += " lux=@Lux or ";
            }
            if (candidateAuthority.Loc.HasText())
            {
                sql += " loc=@Loc or ";
            }
            if (candidateAuthority.Viaf.HasText())
            {
                sql += " viaf=@Viaf or ";
            }
            if (candidateAuthority.WikiData.HasText())
            {
                sql += " wikidata=@WikiData or ";
            }
            sql = sql.RemoveEnd(" or ")!;

            var authorities = conn.Query<Authority>(sql, new
            {
                candidateAuthority.Ulan,
                candidateAuthority.Aat,
                candidateAuthority.Lux,
                candidateAuthority.Loc,
                candidateAuthority.Viaf,
                candidateAuthority.WikiData
            }).ToList();

            Authority? selectedAuthority;
            if (authorities.Count == 0)
            {
                var localIdentifier = IdMinter.Generate();
                conn.Execute($"insert into authorities (identifier, type, label, ulan, aat, lux, loc, viaf, wikidata) " +
                             "values (@localIdentifier, @type, @Label, @Ulan, @Aat, @Lux, @Loc, @Viaf, @WikiData)",
                             new
                             {
                                 localIdentifier,
                                 type,
                                 candidateAuthority.Label,
                                 candidateAuthority.Ulan,
                                 candidateAuthority.Aat,
                                 candidateAuthority.Lux,
                                 candidateAuthority.Loc,
                                 candidateAuthority.Viaf,
                                 candidateAuthority.WikiData
                             });
                selectedAuthority = conn.QuerySingleOrDefault<Authority>(
                    $"select * from authorities where identifier=@localIdentifier", new { localIdentifier });
            }
            else if(authorities.Count == 1)
            {
                // do nothing for now
                selectedAuthority = authorities[0];
            }
            else
            {
                throw new Exception("TODO MERGE");
            }
            // selectedAuthority is not null here
            conn.Execute("update source_string_map set authority=@Identifier " +
                         "where source=@dataSource and string=@sourceString",
                        new { selectedAuthority!.Identifier, dataSource, sourceString });
        }
    }
}
