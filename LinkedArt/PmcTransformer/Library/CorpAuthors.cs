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

            const string corpAuthorDataSource = "corpauthor";
            // reconciliation pass
            foreach (var corpAuthor in corpAuthorDict)
            {
                var corpAuthorString = corpAuthor.Key.Trim().TrimEnd('.').Trim().TrimEnd(',').Trim();
                Console.WriteLine("### " + corpAuthorString);
                string? reduced = corpAuthorString.ReduceGroup();
                if (reduced == corpAuthorString) reduced = null;

                counter++;
                var authorityIdentifier = conn.GetAuthorityIdentifier(corpAuthorDataSource, corpAuthorString, true);
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
                    conn.UpsertAuthority(corpAuthorDataSource, corpAuthorString, "Group", knownGroup);
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
                if (firstLoc == null && corpAuthorString.IndexOf(" and ") > 0)
                {
                    locResults = LocClient.SuggestName(corpAuthorString.Replace(" and ", " & "));
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

                var bestMatch = DecideBestCandidate(corpAuthor.Value, corpAuthorString, candidateAuthorities);
                if(bestMatch != null)
                {
                    conn.UpsertAuthority(corpAuthorDataSource, corpAuthorString, "Group", bestMatch);
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

        private static Authority? DecideBestCandidate(List<string> workIds, string term, Dictionary<string, Authority> candidateAuthorities)
        {
            if(candidateAuthorities.Count == 0)
            {
                return null;
            }

            Authority bestCandidate = new();

            var luxMatches = ExtractAuthoritiesBySource("lux", candidateAuthorities);
            var ulanMatches = ExtractAuthoritiesBySource("ulan", candidateAuthorities);
            var viafMatches = ExtractAuthoritiesBySource("viaf", candidateAuthorities);

            var nonLuxMatches = new List<Authority>(viafMatches);
            nonLuxMatches.AddRange(ulanMatches);

            Authority? locMatch = null;
            if(candidateAuthorities.TryGetValue("loc", out Authority? value))
            {
                locMatch = value;
                nonLuxMatches.Add(locMatch);
            }

            // Need to work out when to NOT trust the LUX match.
            // And also whether to trust it, but not assert equivalence to other records, because we don't want to pollute LUX
            // National Museums and Galleries on Merseyside
            // This string yields 4 distinct LUX entries. eg first is Walker Art Gallery - which is _part_ of the above;
            // But.. LUX says the work was published by Walker Art Gallery, PMC says work was published by National Museums and Galleries on Merseyside
            // I can't safely pick Walker here, because I'm changing what PMC is saying... leave that to LUX.
            // lux1   30  500251553   n80119554     Q1536471  124089443           https://lux.collections.yale.edu/data/group/9c0ee296-e226-49da-9306-ad24dedeb82c
            // lux2   13  500290756   nr88004919              122054769           https://lux.collections.yale.edu/data/group/0437cb33-0dd9-4bad-8337-9fe7547a930e
            // lux3   30  500311436   n50078080     Q1586957  121738032           https://lux.collections.yale.edu/data/group/ef8716b1-e13b-43a0-b65e-8e1d464c3599
            // lux4   35  500301595   n88049144     Q1967497  145220201           https://lux.collections.yale.edu/data/group/b87c9ae6-4117-4319-9112-fa5c8901ce2c
            if (luxMatches.Count > 1)
            {
                var scores = luxMatches.ToDictionary(a => a.Lux!, a => 0);

                // Could pick the closest to the original string (the last of the above)
                var closestLabel = FuzzySharp.Process.ExtractOne(term, luxMatches.Select(a => a.Label));
                var closestLabelAuthority = luxMatches.First(a => a.Label == closestLabel.Value);
                scores[closestLabelAuthority.Lux!] = 100;

                // Or the one with the highest score (matches most works)
                var mostWorks = luxMatches.OrderByDescending(a => a.Score).First();
                scores[mostWorks.Lux] = Convert.ToInt32(100 + 100 * (mostWorks.Score / 100.0)); // a little extra weight for more matches


                // Or that best matches the other results - see Scottish Arts Council
                foreach(var lm in luxMatches)
                {
                    int score = 0;
                    if(lm.Ulan.HasText() && ulanMatches.Exists(um => um.Ulan == lm.Ulan))
                    {
                        score += 100;
                    }
                    if(lm.Loc.HasText() && locMatch != null && locMatch.Loc == lm.Loc)
                    {
                        score += 150;
                    }
                    if(lm.Viaf.HasText())
                    {
                        var viafs = viafMatches.Where(vm => vm.Viaf == lm.Viaf);
                        if (viafs.Any())
                        {
                            score += 20;
                            score += viafs.OrderByDescending(vm => vm.Score).First().Score;
                        }
                    }
                    scores[lm.Lux!] = score;
                }

                var luxMatchKey = scores.OrderByDescending(kvp => kvp.Value).First().Key;
                luxMatches = [luxMatches.Single(lm => lm.Lux == luxMatchKey)];
            }

            if(luxMatches.Count == 1 && HasAtLeastOneEquivalent(luxMatches[0]))
            {
                // Best case, only one LUX match to consider (not zero, not multiple)
                var luxMatch = luxMatches[0];
                bestCandidate.Score++;
                bestCandidate.Label = luxMatch.Label;

                if (luxMatch.Ulan != null && ulanMatches.Exists(a => a.Ulan == luxMatch.Ulan))
                {
                    // We matched a ULAN that LUX matches
                    bestCandidate.Score++;
                    bestCandidate.Ulan = luxMatch.Ulan;
                }
                if (luxMatch.Loc != null && locMatch != null && locMatch.Loc == luxMatch.Loc)
                {
                    // LUX loc is the same as oour LOC match
                    bestCandidate.Score++;
                    bestCandidate.Loc = locMatch.Loc;
                    bestCandidate.Label = locMatch.Label;
                }

                if (luxMatch.Viaf != null && viafMatches.Exists(a => a.Viaf == luxMatch.Viaf))
                {
                    // We matched a VIAF that LUX matches
                    bestCandidate.Score++;
                    bestCandidate.Viaf = luxMatch.Viaf;
                }

                if(luxMatch.Loc != null && viafMatches.Exists(a => a.Loc == luxMatch.Loc))
                {
                    // LUX's loc also appears in VIAF LOC
                    if(bestCandidate.Loc != null)
                    {
                        // we have already matched our LOC
                        bestCandidate.Score++;
                    } 
                    else if(locMatch != null)
                    {
                        // There is a locMatch but it is NOT the same as LUX's LOC
                        // ? decrease score?
                    }
                }
                if(luxMatch.Wikidata != null && nonLuxMatches.Exists(a => a.Wikidata == luxMatch.Wikidata))
                {
                    bestCandidate.Score++;
                    bestCandidate.Wikidata = luxMatch.Wikidata;
                }

                if(bestCandidate.Score >= 4)
                {
                    bestCandidate.Lux = luxMatch.Lux;
                    return bestCandidate;
                }
            }
            else
            {
                if (locMatch != null)
                {
                    var viafMatch = viafMatches.FirstOrDefault(a => a.Loc == locMatch.Loc && a.Score > 50);
                    if (viafMatch != null)
                    {
                        bestCandidate.Label = locMatch.Label;
                        bestCandidate.Loc = locMatch.Loc;
                        bestCandidate.Viaf = viafMatch.Viaf;
                        bestCandidate.Score = 2;
                        if (ulanMatches.Count == 1)
                        {
                            bestCandidate.Ulan = ulanMatches[0].Ulan;
                        }
                        return bestCandidate;
                    }
                }
            }


            return null;
        }

        private static bool HasAtLeastOneEquivalent(Authority authority)
        {
            // if the LUX record has nothing to cross-check, is it any use to us?
            // Maybe only if there is no other information
            return authority.Ulan.HasText() || authority.Loc.HasText() || authority.Viaf.HasText();
        }

        private static List<Authority> ExtractAuthoritiesBySource(string source, Dictionary<string, Authority> candidateAuthorities)
        {
            var matches = candidateAuthorities
                .Where(kvp => kvp.Key.StartsWith(source))
                .OrderByDescending(kvp => Convert.ToInt32(kvp.Key.Replace(source, "")))
                .Select(kvp => kvp.Value)
                .ToList();
            return matches;
        }

    }
}
