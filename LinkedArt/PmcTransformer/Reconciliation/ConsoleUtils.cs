
namespace PmcTransformer.Reconciliation
{
    internal class ConsoleUtils
    {
        public static void WriteCandidateAuthorities(string label, Dictionary<string, Authority> candidateAuthorities)
        {
            if (candidateAuthorities.Keys.Count > 0)
            {
                Console.WriteLine($"----- Group: {label} ---------");
                Console.Write("{0,-7}", "key");
                Console.Write("{0,-4}", "sc");
                Console.Write("{0,-12}", "Ulan");
                Console.Write("{0,-14}", "Loc");
                Console.Write("{0,-10}", "Wiki");
                Console.Write("{0,-20}", "Viaf");
                Console.WriteLine("Lux");
                foreach (var kvp in candidateAuthorities)
                {
                    Console.Write("{0,-7}", kvp.Key);
                    Console.Write("{0,-4}", kvp.Value.Score);
                    Console.Write("{0,-12}", kvp.Value.Ulan);
                    Console.Write("{0,-14}", kvp.Value.Loc);
                    Console.Write("{0,-10}", kvp.Value.Wikidata);
                    Console.Write("{0,-20}", kvp.Value.Viaf);
                    Console.WriteLine(kvp.Value.Lux);
                }
                Console.WriteLine($"----- END Group: {label} ---------");
            }
        }

        public static void WriteAuthority(Authority bestMatch)
        {
            Console.WriteLine($"----- MATCH DECIDED ---------");
            Console.Write("{0,-7}", "");
            Console.Write("{0,-4}", bestMatch.Score);
            Console.Write("{0,-12}", bestMatch.Ulan);
            Console.Write("{0,-14}", bestMatch.Loc);
            Console.Write("{0,-10}", bestMatch.Wikidata);
            Console.Write("{0,-20}", bestMatch.Viaf);
            Console.WriteLine(bestMatch.Lux);
        }
    }
}
