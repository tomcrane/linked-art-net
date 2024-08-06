using LinkedArtNet;
using PmcTransformer.Helpers;
using Group = LinkedArtNet.Group;
using Dapper;
using PmcTransformer.Reconciliation;

namespace PmcTransformer.Library
{
    public class GroupReconciler
    {
        private readonly AuthorityService authorityService;

        public GroupReconciler(AuthorityService authorityService)
        {
            this.authorityService = authorityService;
        }

        /// <summary>
        /// The list values in corpAuthorDict are keys in allWorks
        /// </summary>
        /// <param name="allWorks">Works by identifier</param>
        /// <param name="agents">The original strings from records, to identifier</param>
        public async Task ReconcileGroups(
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
                string? reduced = agent.NormalisedOriginal.ReduceGroup();
                if (reduced == agent.NormalisedOriginal) reduced = null;

                counter++;
                var authorityIdentifier = conn.GetAuthorityIdentifier(dataSource, agent.NormalisedOriginal, true);
                //if(authorityIdentifier!.Authority.HasText())
                //{
                //    Console.WriteLine("Already authorised: " + corpAuthorString);
                //    continue;
                //}
                if (authorityIdentifier!.Processed != null) // can have specific dates later
                {
                    Console.WriteLine("Already attempted: " + agent.NormalisedOriginal);
                    continue;
                }

                var knownGroup = KnownAuthorities.GetGroup(agent.NormalisedOriginal);
                if (knownGroup != null)
                {
                    Console.WriteLine($"Resolved '{agent.NormalisedOriginal}' from known Authorities");
                    conn.UpsertAuthority(dataSource, agent.NormalisedOriginal, "Group", knownGroup);
                    conn.UpdateTimestamp(authorityIdentifier);
                    continue;
                }

                List<Task<Dictionary<string, Authority>>> authTasks = [
                    authorityService.AddCandidatesFromLux(allWorks, agent),
                    authorityService.AddCandidatesFromUlan(agent.NormalisedOriginal, reduced),
                    authorityService.AddCandidatesFromViaf("local.corporateNames all ", agent.NormalisedOriginal),
                    authorityService.AddCandidatesFromLoc(agent.NormalisedOriginal, reduced)
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

            // exact match: Matched 709 of 4888
            // trimmed '.': Matched 759 of 4888
            // after only groups: 499 :(
            Console.WriteLine($"Matched {matches} of {counter}");

            // assignment pass

        }



        public static void AssignCorpAuthors(Dictionary<string, LinguisticObject> allWorks, Dictionary<string, ParsedAgent> corpAuthorDict)
        {
            // Create Groups for corpauthor and assert in book record.
            // TODO - this needs to be consistent between runs so once we are sure about our corporation,
            // mint a permanent id for it and store in DB
            int corpIdMinter = 1;
            var conn = DbCon.Get();

            foreach (var corpAuthor in corpAuthorDict)
            {
                var corpAuthorString = corpAuthor.Key.NormaliseForGroup();
                Group? groupRef = null;
                if (corpAuthorString.StartsWith(Locations.PhotoArchiveName))
                {
                    groupRef = Locations.PhotoArchiveGroupRef;
                }
                else
                {
                    var authorityIdentifier = conn.GetAuthorityIdentifier("corpauthor", corpAuthorString, false);
                    if (authorityIdentifier?.Authority != null)
                    {
                        var authority = conn.QueryFirstOrDefault<Authority>(
                            "select * from authorities where identifier=@Identifier",
                            new { Identifier = authorityIdentifier.Authority });
                        if (authority != null)
                        {
                            groupRef = authority.GetReference() as Group;
                        }
                    }
                    // need the full group if we mint/reconcile here
                    if (groupRef == null)
                    {
                        // we DON'T want to get here - we need to create a non-aligned group with onlu a local identifier
                        groupRef = new Group()
                            .WithId(Identity.GroupBase + corpIdMinter++)
                            .WithLabel(corpAuthorString);
                    }
                }

                foreach (var id in corpAuthor.Value.Identifiers)
                {
                    var work = allWorks[id];
                    work.CreatedBy ??= new Activity(Types.Creation);
                    work.CreatedBy.Part ??= [];
                    work.CreatedBy.Part.Add(new Activity(Types.Creation)
                    {
                        CarriedOutBy = [groupRef]
                    });
                }
            }
        }



    }
}
