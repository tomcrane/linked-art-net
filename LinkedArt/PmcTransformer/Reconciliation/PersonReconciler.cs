using LinkedArtNet;
using PmcTransformer.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PmcTransformer.Reconciliation
{
    public class PersonReconciler
    {
        private readonly LuxClient luxClient;
        private readonly LocClient locClient;
        private readonly UlanClient ulanClient;
        private readonly ViafClient viafClient;
        private readonly WikidataClient wikidataClient;
        private readonly AuthorityService authorityService;

        public PersonReconciler(
            LuxClient luxClient,
            UlanClient ulanClient,
            ViafClient viafClient,
            LocClient locClient,
            WikidataClient wikidataClient,
            AuthorityService authorityService)
        {
            this.luxClient = luxClient;
            this.authorityService = authorityService;
            this.ulanClient = ulanClient;
            this.viafClient = viafClient;
            this.locClient = locClient;
            this.wikidataClient = wikidataClient;
        }

        /// <summary>
        /// The list values in corpAuthorDict are keys in allWorks
        /// </summary>
        /// <param name="allWorks">Works by identifier</param>
        /// <param name="agents">The original strings from records, to identifier</param>
        public async Task ReconcilePeople(
            Dictionary<string, LinguisticObject> allWorks,
            Dictionary<string, ParsedAgent> agents,
            string dataSource)
        {
            var conn = DbCon.Get();

            int matches = 0;
            int counter = 0;

            // reconciliation pass
            foreach (var agentKvp in agents)
            {
                var agent = agentKvp.Value;
                Console.WriteLine();
                Console.WriteLine("### " + agent.NormalisedOriginal);

                counter++;
                var authorityIdentifier = conn.GetAuthorityIdentifier(dataSource, agent.NormalisedOriginal, true);

                if (authorityIdentifier!.Processed != null) // can have specific dates later
                {
                    Console.WriteLine("Already attempted: " + agent.NormalisedOriginal);
                    continue;
                }

                var knownPerson = KnownAuthorities.GetPerson(agent.NormalisedOriginal);
                if (knownPerson != null)
                {
                    Console.WriteLine($"Resolved '{agent.NormalisedOriginal}' from known Authorities");
                    conn.UpsertAuthority(dataSource, agent.NormalisedOriginal, "Person", knownPerson);
                    conn.UpdateTimestamp(authorityIdentifier);
                    continue;
                }


                List<Task<Dictionary<string, Authority>>> authTasks = [
                    authorityService.AddCandidatesFromLux(allWorks, agent),
                    authorityService.AddCandidatesFromUlan(agent.NormalisedName),
                    authorityService.AddCandidatesFromViaf("local.personalNames all ", agent.NormalisedLocForm),
                    authorityService.AddCandidatesFromLoc(agent.NormalisedLocForm)
                ];

                await Task.WhenAll(authTasks);

                List<Dictionary<string, Authority>> allSources = [
                    authTasks[0].Result,
                    authTasks[1].Result,
                    authTasks[2].Result,
                    authTasks[3].Result
                ];

                var candidateAuthorities = allSources.SelectMany(dict => dict).ToDictionary();

                ConsoleUtils.WriteCandidateAuthorities(agent, candidateAuthorities);
                var bestMatch = authorityService.DecideBestCandidate(agentKvp.Value.Identifiers, agent.NormalisedOriginal, candidateAuthorities);
                if (bestMatch != null)
                {
                    matches++;
                    ConsoleUtils.WriteAuthority(bestMatch);
                    conn.UpsertAuthority(dataSource, agent.NormalisedOriginal, "Group", bestMatch);
                }

                conn.UpdateTimestamp(authorityIdentifier);
            }
        }
    }
}
