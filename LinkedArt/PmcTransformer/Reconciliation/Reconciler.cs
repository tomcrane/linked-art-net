﻿using LinkedArtNet;
using PmcTransformer.Helpers;
using Group = LinkedArtNet.Group;
using Dapper;

namespace PmcTransformer.Reconciliation
{
    public class Reconciler
    {
        private readonly AuthorityService authorityService;

        public Reconciler(AuthorityService authorityService)
        {
            this.authorityService = authorityService;
        }

        public async Task Reconcile(
            Dictionary<string, LinguisticObject> allWorks,
            Dictionary<string, List<string>> names,
            string dataSource,
            string authorityType,
            bool expectType)
        {
            var conn = DbCon.Get();

            int matches = 0;
            int counter = 0;

            foreach (var nameKvp in names)
            {
                Console.WriteLine();
                Console.WriteLine("### " + nameKvp.Key);

                counter++;
                var authorityIdentifier = conn.GetAuthorityIdentifier(dataSource, nameKvp.Key, true);
                if (authorityIdentifier!.Processed != null) // can have specific dates later
                {
                    Console.WriteLine("Already attempted: " + nameKvp.Key);
                    continue;
                }

                Authority? knownAuthority = null;
                string? tryFirst = null;
                string? variant = null;
                string? viafQualifier = null;

                switch (authorityType)
                {
                    case "Place":
                        viafQualifier = "local.geographicNames all ";
                        tryFirst = nameKvp.Key;
                        break;
                    case "Concept":

                        // https://id.loc.gov/authorities/subjects/suggest/?q=Physics
                        //                                ^^^^^^^^
                        // but could also be person or group or place as subject
                        knownAuthority =
                            KnownAuthorities.GetPerson(nameKvp.Key) ??
                            KnownAuthorities.GetGroup(nameKvp.Key);
                        viafQualifier = "local.names all ";
                        tryFirst = nameKvp.Key;
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported reconciliation type {authorityType}, expected Place or Concept");
                }
                if (knownAuthority != null)
                {
                    Console.WriteLine($"Resolved '{nameKvp.Key}' from known Authorities");
                    conn.UpsertAuthority(dataSource, nameKvp.Key, authorityType, knownAuthority);
                    conn.UpdateTimestamp(authorityIdentifier);
                    continue;
                }

                if (variant == tryFirst) variant = null;

                List<Task<Dictionary<string, Authority>>> authTasks = [
                    authorityService.AddCandidatesFromLux(authorityType, tryFirst),
                    authorityService.AddCandidatesFromUlan(tryFirst, variant),
                    authorityService.AddCandidatesFromViaf(viafQualifier, tryFirst),
                    authorityService.AddCandidatesFromLoc(tryFirst, variant)
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
                    conn.UpsertAuthority(dataSource, agent.NormalisedOriginal, authorityType, bestMatch);
                }

                conn.UpdateTimestamp(authorityIdentifier);
            }
        }

        /// <summary>
        /// The list values in corpAuthorDict are keys in allWorks
        /// </summary>
        /// <param name="allWorks">Works by identifier</param>
        /// <param name="agents">The original strings from records, to identifier</param>
        public async Task Reconcile(
            Dictionary<string, LinguisticObject> allWorks,
            Dictionary<string, ParsedAgent> agents,
            string dataSource,
            string authorityType,
            bool expectType)
        {
            var conn = DbCon.Get();

            int matches = 0;
            int counter = 0;

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

                Authority? knownAuthority = null;
                string? tryFirst = null;
                string? variant = null;
                string? viafQualifier = null;

                switch (authorityType)
                {
                    case "Person":
                        knownAuthority = KnownAuthorities.GetPerson(agent.NormalisedOriginal);
                        tryFirst = agent.NormalisedLocForm;
                        variant = agent.Name;
                        viafQualifier = "local.personalNames all ";
                        break;
                    case "Group":
                        knownAuthority = KnownAuthorities.GetGroup(agent.NormalisedOriginal);
                        tryFirst = agent.NormalisedLocForm;
                        variant = agent.NormalisedOriginal.ReduceGroup();
                        viafQualifier = "local.corporateNames all ";
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported reconciliation type {authorityType}, expected Person or Group");
                }
                if (knownAuthority != null)
                {
                    Console.WriteLine($"Resolved '{agent.NormalisedOriginal}' from known Authorities");
                    conn.UpsertAuthority(dataSource, agent.NormalisedOriginal, authorityType, knownAuthority);
                    conn.UpdateTimestamp(authorityIdentifier);
                    continue;
                }

                if (variant == tryFirst) variant = null;

                List<Task<Dictionary<string, Authority>>> authTasks = [
                    authorityService.AddCandidatesFromLux(allWorks, agent),
                    authorityService.AddCandidatesFromUlan(tryFirst, variant),
                    authorityService.AddCandidatesFromViaf(viafQualifier, tryFirst),
                    authorityService.AddCandidatesFromLoc(tryFirst, variant)
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
                    conn.UpsertAuthority(dataSource, agent.NormalisedOriginal, authorityType, bestMatch);
                }

                conn.UpdateTimestamp(authorityIdentifier);
            }
        }
    }
}
