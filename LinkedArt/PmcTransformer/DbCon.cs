using Dapper;
using Npgsql;

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
            string tableName, string source, bool createEntryForSource = false)
        {
            var ssa = conn.QueryFirstOrDefault<SourceStringAndAuthority>(
                $"select source_string, authority from {tableName} where source_string=@SourceString",
                new { SourceString = source });
            if(ssa == null && createEntryForSource)
            {
                Console.WriteLine($"Inserting row into {tableName} for {source}");
                conn.Execute($"insert into {tableName} (source_string, authority) values (@Source, null)", new { Source = source });
            }
            return ssa?.Authority;
        }
    }
}
