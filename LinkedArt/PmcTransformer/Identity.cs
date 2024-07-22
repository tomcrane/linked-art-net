using Dapper;
using Npgsql;
using System.Text;

namespace PmcTransformer
{
    public class Identity
    {
        public const string BaseUrl = "https://data.paul-mellon-centre.ac.uk/";

        // Library
        public const string LibraryLinguistic = $"{BaseUrl}library/work/";
        public const string LibraryHmo = $"{BaseUrl}library/object/";

        // Archive
        public const string ArchiveRecord = $"{BaseUrl}archive/";
        public const string ArchiveAuthority = $"{BaseUrl}archive-authority/"; // for interim reconciliation

        public const string GroupBase = $"{BaseUrl}group/";
        public const string PeopleBase = $"{BaseUrl}person/";
        public const string PlaceBase = $"{BaseUrl}place/";
        public const string ConceptBase = $"{BaseUrl}concept/";
    }

    public class IdMinter
    {
        private static readonly char[] Numbers = "23456789".ToCharArray();                   // not 1, 0
        private static readonly char[] Letters = "abcdefghjkmnpqrstuvwxyz".ToCharArray();    // not i, l, o
        private static readonly char[] All = [.. Numbers, .. Letters];

        public static string Generate(int length = 8, bool letterFirst = true)
        {
            Random random = new();
            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                if (letterFirst && i == 0)
                {
                    sb.Append(Letters[random.Next(Letters.Length)]);
                }
                else
                {
                    sb.Append(All[random.Next(All.Length)]);
                }
            }
            return sb.ToString();
        }

        public static string Generate(NpgsqlConnection conn)
        {
            string? candidate = null;
            while (candidate == null || IdExistsAlready(candidate, conn))
            {
                candidate = Generate();
            }
            return candidate;
        }

        private static bool IdExistsAlready(string candidate, NpgsqlConnection conn)
        {
            return conn.ExecuteScalar<bool>("select count(1) from authorities where identifier=@id", new { id = candidate });
        }
    }
}
