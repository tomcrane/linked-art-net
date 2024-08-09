using Dapper;
using LinkedArtNet;
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

        public static List<Authority> FindByEquivalence(this NpgsqlConnection conn, LinkedArtObject laObj)
        {
            var testAuthority = new Authority
            {
                Ulan = laObj.Equivalent?.FirstOrDefault(x => x.Id.StartsWith(Authority.UlanPrefix))?.Id?.LastPathElement(),
                Viaf = laObj.Equivalent?.FirstOrDefault(x => x.Id.StartsWith(Authority.ViafPrefix))?.Id?.LastPathElement(),
                Loc = laObj.Equivalent?.FirstOrDefault(x => x.Id.StartsWith(Authority.LocPrefix))?.Id?.LastPathElement(),
                Wikidata = laObj.Equivalent?.FirstOrDefault(x => x.Id.StartsWith(Authority.WikidataPrefix))?.Id?.LastPathElement(),
                Aat = laObj.Equivalent?.FirstOrDefault(x => x.Id.StartsWith(Authority.AatPrefix))?.Id?.LastPathElement(),
                Lux = laObj.Equivalent?.FirstOrDefault(x => x.Id.StartsWith(Authority.LuxPrefix))?.Id
            };

            var matching = conn.SelectFromEquivalents(testAuthority);
            if(matching.Count > 1)
            {
                Console.WriteLine("More than one match for " + laObj.Label);
            }
            if(matching.Count > 0)
            {
                return matching;
            }

            // didn't find a direct match, but can we match on name?
            // TODO
            return new List<Authority>();


        }

        public static AuthorityStringWithSource? GetAuthorityIdentifier(this NpgsqlConnection conn,
            string source, string s, bool createEntryForSource = false, bool createUnreconciledIdentifier = false)
        {
            var ssa = conn.QueryFirstOrDefault<AuthorityStringWithSource>(
                "select source, string, authority, processed, unreconciled_authority from source_string_map " +
                "where source=@source and string=@s", new { source, s });
            if(ssa == null && createEntryForSource)
            {
                conn.Execute($"insert into source_string_map (source, string, authority) " +
                              "values (@source, @s, null)", new { source, s });
            }
            if(ssa?.UnreconciledAuthority == null && createUnreconciledIdentifier)
            {
                var unrec = "unrec_" + IdMinter.Generate();

                conn.Execute($"update source_string_map set unreconciled_authority=@unrec " +
                             $"where source=@source and string=@s and unreconciled_authority is null ", 
                             new { unrec }); 
                ssa = conn.QueryFirstOrDefault<AuthorityStringWithSource>(
                "select source, string, authority, processed, unreconciled_authority from source_string_map " +
                "where source=@source and string=@s", new { source, s });
            }
            return ssa ?? new AuthorityStringWithSource { Source=source, String=s };
        }

        public static Authority? GetAuthorityFromSourceString(
            this NpgsqlConnection conn, string source, string s, bool createUnreconciled)
        {
            var authorityIdentifier = conn.GetAuthorityIdentifier(source, s, false, createUnreconciled);

            if (authorityIdentifier?.Authority != null)
            {
                var authority = conn.QueryFirstOrDefault<Authority>(
                    "select * from authorities where identifier=@Identifier",
                    new { Identifier = authorityIdentifier.Authority });
                return authority;
            }
            if(createUnreconciled)
            {
                if (authorityIdentifier?.UnreconciledAuthority == null)
                {
                    throw new Exception("Must have either a real authority or an unreconciled authority");
                }
                return new Authority
                {
                    Identifier = authorityIdentifier.UnreconciledAuthority,
                    Unreconciled = true
                };
            }
            return null;
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
                var localIdentifier = IdMinter.Generate(conn);
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
            List<Authority> authorities = conn.SelectFromEquivalents(candidateAuthority);

            Authority? selectedAuthority;
            if (authorities.Count == 0)
            {
                var localIdentifier = IdMinter.Generate(conn);
                conn.Execute($"insert into authorities (identifier, type, label, ulan, aat, lux, loc, viaf, wikidata, pmc) " +
                             "values (@localIdentifier, @type, @Label, @Ulan, @Aat, @Lux, @Loc, @Viaf, @Wikidata, @Pmc)",
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
                                 candidateAuthority.Wikidata,
                                 candidateAuthority.Pmc
                             });
                selectedAuthority = conn.QuerySingleOrDefault<Authority>(
                    $"select * from authorities where identifier=@localIdentifier", new { localIdentifier });
            }
            else
            {
                bool updateLabel = true;
                if (authorities.Count > 1)
                {
                    Console.WriteLine("Multiple matching authorities we could merge with");
                    var closestLabel = FuzzySharp.Process.ExtractOne(
                        sourceString.RemoveThingsInParens(),
                        authorities.Select(a => a.Label.RemoveThingsInParens()));
                    Console.WriteLine($"Picking {closestLabel.Value} [Score: {closestLabel.Score}]");
                    authorities = [authorities.First(a => a.Label == closestLabel.Value || a.Label.RemoveThingsInParens() == closestLabel.Value)];
                    updateLabel = false;
                }

                selectedAuthority = authorities[0];
                if (candidateAuthority.Label.HasText() && updateLabel)
                {
                    selectedAuthority.Label = candidateAuthority.Label;
                }
                if (candidateAuthority.Ulan.HasText())
                {
                    selectedAuthority.Ulan = candidateAuthority.Ulan;
                }
                if (candidateAuthority.Aat.HasText())
                {
                    selectedAuthority.Aat = candidateAuthority.Aat;
                }
                if (candidateAuthority.Lux.HasText())
                {
                    selectedAuthority.Lux = candidateAuthority.Lux;
                }
                if (candidateAuthority.Loc.HasText())
                {
                    selectedAuthority.Loc = candidateAuthority.Loc;
                }
                if (candidateAuthority.Viaf.HasText())
                {
                    selectedAuthority.Viaf = candidateAuthority.Viaf;
                }
                if (candidateAuthority.Wikidata.HasText())
                {
                    selectedAuthority.Wikidata = candidateAuthority.Wikidata;
                }
                if (candidateAuthority.Pmc.HasText())
                {
                    selectedAuthority.Pmc = candidateAuthority.Pmc;
                }

                conn.Execute($"update authorities set " +
                    $"label=@Label, ulan=@Ulan, aat=@Aat, lux=@Lux, loc=@Loc, viaf=@Viaf, wikidata=@Wikidata, pmc=@Pmc " +
                    $"where identifier=@Identifier",
                    new
                    {
                        selectedAuthority.Label,
                        selectedAuthority.Ulan,
                        selectedAuthority.Aat,
                        selectedAuthority.Lux,
                        selectedAuthority.Loc,
                        selectedAuthority.Viaf,
                        selectedAuthority.Wikidata,
                        selectedAuthority.Pmc,
                        selectedAuthority.Identifier,
                    });

            }
            // selectedAuthority is not null here
            conn.Execute("update source_string_map set authority=@Identifier " +
                         "where source=@dataSource and string=@sourceString",
                        new { selectedAuthority!.Identifier, dataSource, sourceString });
        }

        private static List<Authority> SelectFromEquivalents(this NpgsqlConnection conn, Authority testAuthority)
        {
            string sql = "select * from authorities where ";
            if (testAuthority.Ulan.HasText())
            {
                sql += " ulan=@Ulan or ";
            }
            if (testAuthority.Aat.HasText())
            {
                sql += " aat=@Aat or ";
            }
            if (testAuthority.Lux.HasText())
            {
                sql += " lux=@Lux or ";
            }
            if (testAuthority.Loc.HasText())
            {
                sql += " loc=@Loc or ";
            }
            if (testAuthority.Viaf.HasText())
            {
                sql += " viaf=@Viaf or ";
            }
            if (testAuthority.Wikidata.HasText())
            {
                sql += " wikidata=@Wikidata or ";
            }
            if (testAuthority.Pmc.HasText())
            {
                sql += " pmc=@Pmc or ";
            }
            sql = sql.RemoveEnd(" or ")!;

            var authorities = conn.Query<Authority>(sql, new
            {
                testAuthority.Ulan,
                testAuthority.Aat,
                testAuthority.Lux,
                testAuthority.Loc,
                testAuthority.Viaf,
                testAuthority.Wikidata,
                testAuthority.Pmc
            }).ToList();
            return authorities;
        }

        public static void UpdateTimestamp(this NpgsqlConnection conn, AuthorityStringWithSource ssa)
        {
            conn.Execute("update source_string_map set processed=@UtcNow " +
                         "where source=@Source and string=@String",
                        new { ssa.Source, ssa.String, DateTimeOffset.UtcNow });

        }

    }
}
