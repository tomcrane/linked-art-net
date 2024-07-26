using LinkedArtNet;
using PmcTransformer.Helpers;
using Group = LinkedArtNet.Group;
using Dapper;
using PmcTransformer.Reconciliation;

namespace PmcTransformer.Library
{
    public static class CorpAuthors
    {
        /// <summary>
        /// The list values in corpAuthorDict are keys in allWorks
        /// </summary>
        /// <param name="allWorks">Works by identifier</param>
        /// <param name="corpAuthorDict">The original strings from records, to identifier</param>
        public static void ReconcileCorpAuthors(Dictionary<string, LinguisticObject> allWorks, Dictionary<string, List<string>> corpAuthorDict)
        {
            // Create Groups for corpauthor and assert in book record.
            // TODO - this needs to be consistent between runs so once we are sure about our corporation,
            // mint a permanent id for it and store in DB
            int corpIdMinter = 1;
            var conn = DbCon.Get();

            int matches = 0;
            int counter = 0;

            // reconciliation pass
            foreach (var corpAuthor in corpAuthorDict)
            {
                var corpAuthorString = corpAuthor.Key.Trim().TrimEnd('.').Trim().TrimEnd(',').Trim();
                Console.WriteLine("### " + corpAuthorString);
                string? reduced = corpAuthorString.ReduceGroup();
                if (reduced == corpAuthorString) reduced = null;

                counter++;
                var authorityIdentifier = conn.GetAuthorityIdentifier("corpauthor", corpAuthorString, true);
                if(authorityIdentifier!.Authority.HasText())
                {
                    Console.WriteLine("Already authorised: " + corpAuthorString);
                    continue;
                }
                if (authorityIdentifier.Processed != null) // can have specific dates later
                {
                    Console.WriteLine("Already attempted: " + corpAuthorString);
                    continue;
                }

                var knownGroup = KnownAuthorities.GetGroup(corpAuthorString);
                if(knownGroup != null)
                {
                    Console.WriteLine($"Resolved '{corpAuthorString}' from known Authorities");
                    conn.UpsertAuthority("corpauthor", corpAuthorString, "Group", knownGroup);
                    conn.UpdateTimestamp(authorityIdentifier);
                    continue;
                }

                var candidateAuthorities = new Dictionary<string, Authority>();

                // need to find out what this thing is...
                // We can ask LUX for a GROUP that CREATED the work(s)
                var allActors = new List<Actor>();
                Console.WriteLine($"{corpAuthor.Value.Count} works(s) for {corpAuthorString}");
                var workIds = corpAuthor.Value;
                if(workIds.Count > 50)
                {
                    var shuffled = workIds.Select(w => w).ToList();
                    shuffled.Shuffle();
                    workIds = shuffled.Take(50).ToList();
                }
                foreach (var workId in workIds)
                {
                    var work = allWorks[workId];
                    Console.WriteLine($"LUX: actors like '{corpAuthorString}' who created work '{work.Label}'");
                    var actors = LuxClient.ActorsWhoCreatedWorks(corpAuthorString, work.Label!);
                    Console.WriteLine($"{actors.Count} found");
                    allActors.AddRange(actors);
                }
                var distinctActors = allActors.DistinctBy(a => a.Id).ToList();
                var counts = distinctActors.ToDictionary(a => a.Id!, a => allActors.Count(aa => aa.Id == a.Id));
                Console.WriteLine($"*** {distinctActors.Count} DISTINCT actors returned from LUX");
                // Now we have a bunch of Groups (hopefully just 1).
                int luxCounter = 1;
                foreach(var actor in distinctActors)
                {
                    var authority = actor.AuthorityFromEquivalents(corpAuthorString);
                    // This is misleading as it only really makes sense when there are lots of works for the same string
                    // It is NOT a stting distance score
                    authority.Score = Convert.ToInt32(counts[authority.Lux!] / (decimal)workIds.Count * 100);
                    candidateAuthorities[$"lux{luxCounter++}"] = authority;
                }

                // ulans
                // try a simple match first
                var ulans = UlanClient.GetIdentifiersAndLabels(corpAuthorString, "Group");
                if(ulans.Count == 0 && reduced != null)
                {
                    ulans = UlanClient.GetIdentifiersAndLabels(reduced, "Group");
                }
                int ulanCounter = 1;
                foreach (var ulanMatch  in ulans)
                {
                    var authority = new Authority { Ulan = ulanMatch.Identifier, Label = ulanMatch.Label };
                    candidateAuthorities[$"ulan{ulanCounter++}"] = authority;
                }


                var viafAuthorities = ViafClient.SearchBestMatch("local.corporateNames all ", corpAuthorString);
                int viafCounter = 1;
                foreach (var viafAuthority in viafAuthorities)
                {
                    candidateAuthorities[$"viaf{viafCounter++}"] = viafAuthority;
                }

                var locResults = LocClient.SuggestName(corpAuthorString);
                var firstLoc = locResults.FirstOrDefault();
                if (firstLoc == null && reduced != null)
                {
                    locResults = LocClient.SuggestName(reduced);
                    firstLoc = locResults.FirstOrDefault();
                }
                if (firstLoc != null)
                {
                    var authority = new Authority { Loc = firstLoc.Identifier, Label = firstLoc.Label };
                    candidateAuthorities["loc"] = authority;
                }

                // Now we have multiple authorities. Lets see if they agree with each other.
                if(candidateAuthorities.Keys.Count > 0)
                {
                    Console.WriteLine("--------------");
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
                    Console.WriteLine("--------------");
                }

                conn.UpdateTimestamp(authorityIdentifier);
            }

            // exact match: Matched 709 of 4888
            // trimmed '.': Matched 759 of 4888
            // after only groups: 499 :(
            Console.WriteLine($"Matched {matches} of {counter}");


            // assignment pass
            foreach (var corpAuthor in corpAuthorDict)
            {
                var corpAuthorString = corpAuthor.Key.Trim().TrimEnd('.');
                Group? groupRef = null;
                if(corpAuthorString.StartsWith(Locations.PhotoArchiveName))
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
                        if(authority != null)
                        {
                            groupRef = authority.GetReference() as Group;
                        }
                    }
                    // map the strings into a local "DB" (dict)
                    // build the dict by reconciling against local postgres of against LUX if no match

                    // save, so that reruns don't re-query


                    // need the full group if we mint/reconcile here
                    if (groupRef == null)
                    {
                        // we DON'T want to get here - we need to create a non-aligned group with onlu a local identifier
                        groupRef = new Group()
                            .WithId(Identity.GroupBase + corpIdMinter++)
                            .WithLabel(corpAuthorString);
                    }
                }

                foreach (var id in corpAuthor.Value)
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
