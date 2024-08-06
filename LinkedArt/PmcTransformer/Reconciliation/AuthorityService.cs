using LinkedArtNet;
using PmcTransformer.Helpers;

namespace PmcTransformer.Reconciliation
{
    public class AuthorityService
    {
        private readonly LuxClient luxClient;
        private readonly LocClient locClient;
        private readonly UlanClient ulanClient;
        private readonly ViafClient viafClient;
        private readonly WikidataClient wikidataClient;

        public AuthorityService(
            LuxClient luxClient,
            UlanClient ulanClient,
            ViafClient viafClient,
            LocClient locClient,
            WikidataClient wikidataClient)
        {
            this.luxClient = luxClient;
            this.ulanClient = ulanClient;
            this.viafClient = viafClient;
            this.locClient = locClient;
            this.wikidataClient = wikidataClient;
        }

        public async Task<Authority> GetFromEquivalents(LinkedArtObject laObj, string? disambiguator = null)
        {
            var authority = new Authority
            {
                Label = laObj.GetPrimaryName(true)
            };
            const string lux = "https://lux.collections.yale.edu/";
            if (laObj.Id!.StartsWith(lux))
            {
                authority.Lux = laObj.Id;
            }
            if (laObj.Equivalent != null && laObj.Equivalent.Count != 0)
            {
                // for each of our authority providers (loc, viaf etc), if there's only one equivalent then use that one.
                // if there is more than one, we need to find their source labels and attempt to match on disambiguator

                const string locPrefix = "http://id.loc.gov/authorities/names/";
                var locs = laObj.Equivalent.Where(e => e.Id.StartsWith(locPrefix)).ToList();
                if (locs.Count == 1)
                {
                    authority.Loc = locs[0].Id.Replace(locPrefix, "");
                }
                else if (locs.Count > 1)
                {
                    Console.WriteLine($"{locs.Count} Multiple LOC equivalents");
                    var idsAndLabels = new List<IdentifierAndLabel>();
                    foreach(var l in locs)
                    {
                        var idAndLabel = await locClient.GetName(l.Id.Replace(locPrefix, ""));
                        idsAndLabels.Add(idAndLabel);
                    }
                    var bestMatch = FuzzySharp.Process.ExtractOne(disambiguator, idsAndLabels.Select(i => i.Label));
                    Console.WriteLine("LOC Candidates:");
                    idsAndLabels.ForEach(Console.WriteLine);
                    Console.WriteLine($"Best: {bestMatch.Value}; Score: {bestMatch.Score}");
                    var bestIdAndLabel = idsAndLabels[bestMatch.Index];
                    authority.Loc = bestIdAndLabel.Identifier;
                }

                const string viafPrefix = "http://viaf.org/viaf/";
                var viafs = laObj.Equivalent.Where(e => e.Id.StartsWith(viafPrefix)).ToList();
                if (viafs.Count == 1)
                {
                    authority.Viaf = viafs[0].Id.Replace(viafPrefix, "");
                }
                else if (viafs.Count > 1)
                {
                    Console.WriteLine($"{viafs.Count} Multiple VIAF equivalents");
                    var idsAndLabelsAll = new List<IdentifierAndLabel>();
                    foreach(var v in viafs)
                    {
                        var idAndLabel = await viafClient.GetName(v.Id.Replace(viafPrefix, ""));
                        idsAndLabelsAll.Add(idAndLabel);
                    }
                    var idsAndLabels = idsAndLabelsAll
                        .Where(x => x != null && x.Label.HasText())
                        .ToList();
                    var bestMatch = FuzzySharp.Process.ExtractOne(disambiguator, idsAndLabels.Select(i => i.Label));
                    Console.WriteLine("VIAF Candidates:");
                    idsAndLabels.ForEach(Console.WriteLine);
                    Console.WriteLine($"Best: {bestMatch.Value}; Score: {bestMatch.Score}");
                    var bestIdAndLabel = idsAndLabels[bestMatch.Index];
                    authority.Viaf = bestIdAndLabel.Identifier;
                }

                const string ulanPrefix = "http://vocab.getty.edu/ulan/";
                var ulans = laObj.Equivalent.Where(e => e.Id.StartsWith(ulanPrefix)).ToList();
                if (ulans.Count == 1)
                {
                    authority.Ulan = ulans[0].Id.Replace(ulanPrefix, "");
                }
                else if (ulans.Count > 1)
                {
                    Console.WriteLine($"{ulans.Count} Multiple ULAN equivalents");
                    var actors = new List<Actor>();
                    foreach(var u in ulans)
                    {
                        var actor = await ulanClient.GetFromIdentifier(u.Id.Replace(ulanPrefix, ""));
                        actors.Add(actor);
                    }
                    var bestMatch = FuzzySharp.Process.ExtractOne(disambiguator, actors.Select(a => a.Label));
                    Console.WriteLine("ULAN Candidates:");
                    actors.ForEach(a => Console.WriteLine(a.Label));
                    Console.WriteLine($"Best: {bestMatch.Value}; Score: {bestMatch.Score}");
                    var bestActor = actors[bestMatch.Index];
                    authority.Ulan = bestActor.Id.Replace(ulanPrefix, "");
                }

                const string wikiPrefix = "http://www.wikidata.org/entity/";
                var wkds = laObj.Equivalent.Where(e => e.Id.StartsWith(wikiPrefix)).ToList();
                if (wkds.Count == 1)
                {
                    authority.Wikidata = wkds[0].Id.Replace(wikiPrefix, "");
                }
                else if (wkds.Count > 1)
                {
                    Console.WriteLine($"{wkds.Count} Multiple Wikidata equivalents");
                    var idsAndLabels = new List<IdentifierAndLabel>();
                    foreach (var w in wkds)
                    {
                        var idAndLabel = await wikidataClient.GetName(w.Id.Replace(wikiPrefix, ""));
                        idsAndLabels.Add(idAndLabel);
                    }
                    var bestMatch = FuzzySharp.Process.ExtractOne(disambiguator, idsAndLabels.Select(i => i.Label));
                    Console.WriteLine("Wikidata Candidates:");
                    idsAndLabels.ForEach(Console.WriteLine);
                    Console.WriteLine($"Best: {bestMatch.Value}; Score: {bestMatch.Score}");
                    var bestIdAndLabel = idsAndLabels[bestMatch.Index];
                    authority.Wikidata = bestIdAndLabel.Identifier;
                }


                // now same for other sources.

            }
            return authority;
        }


        public async Task<Dictionary<string, Authority>> AddCandidatesFromLoc(string? agentString, string? variant = null)
        {
            var candidateAuthorities = new Dictionary<string, Authority>();
            if(string.IsNullOrWhiteSpace(agentString))
            {
                return candidateAuthorities;
            }
            var locResults = await locClient.SuggestName(agentString);
            var firstLoc = locResults.FirstOrDefault();
            if (firstLoc == null && variant != null)
            {
                locResults = await locClient.SuggestName(variant);
                firstLoc = locResults.FirstOrDefault();
            }
            if (firstLoc == null && agentString.IndexOf(" and ") > 0)
            {
                locResults = await locClient.SuggestName(agentString.Replace(" and ", " & "));
                firstLoc = locResults.FirstOrDefault();
            }
            if (firstLoc != null)
            {
                var authority = new Authority { Loc = firstLoc.Identifier, Label = firstLoc.Label };
                candidateAuthorities["loc"] = authority;
            }
            return candidateAuthorities;
        }

        public async Task<Dictionary<string, Authority>> AddCandidatesFromViaf(string prefix, string? agentString)
        {
            var candidateAuthorities = new Dictionary<string, Authority>();
            if(string.IsNullOrWhiteSpace(agentString))
            {
                return candidateAuthorities;
            }
            var viafAuthorities = await viafClient.SearchBestMatch(prefix, agentString);
            int viafCounter = 1;
            foreach (var viafAuthority in viafAuthorities)
            {
                candidateAuthorities[$"viaf{viafCounter++}"] = viafAuthority;
            }
            return candidateAuthorities;
        }

        public async Task<Dictionary<string, Authority>> AddCandidatesFromUlan(string? agentString, string? variant = null)
        {
            var candidateAuthorities = new Dictionary<string, Authority>();
            if(string.IsNullOrWhiteSpace(agentString))
            {
                return candidateAuthorities;
            }
            // ulans
            // try a simple match first
            const int requiredPercentageMatch = 94; // This is a percentage of chars in the word
            var ulansLevAll = await ulanClient.GetIdentifiersAndLabelsLevenshtein(agentString, "Group", 5);
            var ulansLevPassing = ulansLevAll
                .Where(x => x.Score >= requiredPercentageMatch)
                .ToList();
            if (ulansLevPassing.Count == 0 && variant != null)
            {
                ulansLevAll = await ulanClient.GetIdentifiersAndLabelsLevenshtein(variant, "Group", 5);
                ulansLevPassing = ulansLevAll
                    .Where(x => x.Score >= requiredPercentageMatch)
                    .ToList();
            }
            int ulanCounter = 1;
            foreach (var ulanMatch in ulansLevPassing)
            {
                var authority = new Authority
                {
                    Ulan = ulanMatch.Identifier,
                    Label = ulanMatch.Label,
                    Score = ulanMatch.Score
                };
                candidateAuthorities[$"ulan{ulanCounter++}"] = authority;
            }
            return candidateAuthorities;
        }

        public async Task<Dictionary<string, Authority>> AddCandidatesFromLux(
            Dictionary<string, LinguisticObject> allWorks, 
            ParsedAgent agent)
        {
            var candidateAuthorities = new Dictionary<string, Authority>();
            if(string.IsNullOrWhiteSpace(agent.NormalisedLocForm))
            {
                return candidateAuthorities;
            }
            // need to find out what this thing is...
            // We can ask LUX for a GROUP that CREATED the work(s)
            var allActors = new List<Actor>();
            var workIds = agent.Identifiers;
            Console.WriteLine($"{workIds.Count} works(s) for {agent.NormalisedLocForm}");
            if (workIds.Count > 50)
            {
                var shuffled = workIds.Select(w => w).ToList();
                shuffled.Shuffle();
                workIds = shuffled.Take(50).ToList();
            }
            foreach (var workId in workIds)
            {
                var work = allWorks[workId];
                Console.WriteLine($"LUX: actors like '{agent.NormalisedLocForm}' who created work '{work.Label}'");
                var actors = await luxClient.ActorsWhoCreatedWorks(agent.NormalisedLocForm, work.Label!);
                Console.WriteLine($"{actors.Count} found");
                allActors.AddRange(actors);
            }
            var distinctActors = allActors.DistinctBy(a => a.Id).ToList();
            var counts = distinctActors.ToDictionary(a => a.Id!, a => allActors.Count(aa => aa.Id == a.Id));
            Console.WriteLine($"*** {distinctActors.Count} DISTINCT actors returned from LUX");
            // Now we have a bunch of Groups (hopefully just 1).
            int luxCounter = 1;
            foreach (var actor in distinctActors)
            {
                var authority = await GetFromEquivalents(actor, agent.NormalisedLocForm);
                // This is misleading as it only really makes sense when there are lots of works for the same string
                // It is NOT a stting distance score
                authority.Score = Convert.ToInt32(counts[authority.Lux!] / (decimal)workIds.Count * 100);
                candidateAuthorities[$"lux{luxCounter++}"] = authority;
            }
            return candidateAuthorities;
        }




        public Authority? DecideBestCandidate(List<string> workIds, string term, Dictionary<string, Authority> candidateAuthorities)
        {
            if (candidateAuthorities.Count == 0)
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
            if (candidateAuthorities.TryGetValue("loc", out Authority? value))
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
                foreach (var lm in luxMatches)
                {
                    int score = 0;
                    if (lm.Ulan.HasText() && ulanMatches.Exists(um => um.Ulan == lm.Ulan))
                    {
                        score += 100 + lm.Score;
                    }
                    if (lm.Loc.HasText() && locMatch != null && locMatch.Loc == lm.Loc)
                    {
                        score += 150;
                    }
                    if (lm.Viaf.HasText())
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

            if (luxMatches.Count == 1 && HasAtLeastOneEquivalent(luxMatches[0]))
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

                if (luxMatch.Loc != null && viafMatches.Exists(a => a.Loc == luxMatch.Loc))
                {
                    // LUX's loc also appears in VIAF LOC
                    if (bestCandidate.Loc != null)
                    {
                        // we have already matched our LOC
                        bestCandidate.Score++;
                    }
                    else if (locMatch != null)
                    {
                        // There is a locMatch but it is NOT the same as LUX's LOC
                        // ? decrease score?
                    }
                }
                if (luxMatch.Wikidata != null && nonLuxMatches.Exists(a => a.Wikidata == luxMatch.Wikidata))
                {
                    bestCandidate.Score++;
                    bestCandidate.Wikidata = luxMatch.Wikidata;
                }

                if (bestCandidate.Score >= 3)
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

        public bool HasAtLeastOneEquivalent(Authority authority)
        {
            // if the LUX record has nothing to cross-check, is it any use to us?
            // Maybe only if there is no other information
            return authority.Ulan.HasText() || authority.Loc.HasText() || authority.Viaf.HasText();
        }

        public List<Authority> ExtractAuthoritiesBySource(string source, Dictionary<string, Authority> candidateAuthorities)
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
