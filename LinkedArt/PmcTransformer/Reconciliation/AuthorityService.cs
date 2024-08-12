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
                Label = laObj.GetPrimaryName(true),
                Type = laObj.Type
            };
            if (laObj.Id!.StartsWith(Authority.LuxPrefix))
            {
                authority.Lux = laObj.Id;
            }
            if (laObj.Equivalent != null && laObj.Equivalent.Count != 0)
            {
                // for each of our authority providers (loc, viaf etc), if there's only one equivalent then use that one.
                // if there is more than one, we need to find their source labels and attempt to match on disambiguator

                var locs = laObj.Equivalent.Where(e => e.Id.StartsWith(Authority.LocPrefix)).ToList();
                if (locs.Count == 1)
                {
                    authority.Loc = locs[0].Id.Split('/')[^1];
                }
                else if (locs.Count > 1)
                {
                    Console.WriteLine($"{locs.Count} Multiple LOC equivalents");
                    var idsAndLabels = new List<IdentifierAndLabel>();
                    foreach(var l in locs)
                    {
                        var idParts = l.Id.Split('/');  
                        var idAndLabel = await locClient.GetName(idParts[^2], idParts[^1]);
                        if (idAndLabel != null)
                        {
                            idsAndLabels.Add(idAndLabel);
                        }
                    }
                    var bestMatch = FuzzySharp.Process.ExtractOne(disambiguator, idsAndLabels.Select(i => i.Label));
                    Console.WriteLine("LOC Candidates:");
                    idsAndLabels.ForEach(Console.WriteLine);
                    Console.WriteLine($"Best: {bestMatch.Value}; Score: {bestMatch.Score}");
                    var bestIdAndLabel = idsAndLabels[bestMatch.Index];
                    authority.Loc = bestIdAndLabel.Identifier;
                }

                var viafs = laObj.Equivalent.Where(e => e.Id.StartsWith(Authority.ViafPrefix)).ToList();
                if (viafs.Count == 1)
                {
                    authority.Viaf = viafs[0].Id.Replace(Authority.ViafPrefix, "");
                }
                else if (viafs.Count > 1)
                {
                    Console.WriteLine($"{viafs.Count} Multiple VIAF equivalents");
                    var idsAndLabelsAll = new List<IdentifierAndLabel>();
                    foreach(var v in viafs)
                    {
                        var idAndLabel = await viafClient.GetName(v.Id.Replace(Authority.ViafPrefix, ""));
                        idsAndLabelsAll.Add(idAndLabel);
                    }
                    var idsAndLabels = idsAndLabelsAll
                        .Where(x => x != null && x.Label.HasText())
                        .ToList();
                    var bestMatch = FuzzySharp.Process.ExtractOne(disambiguator, idsAndLabels.Select(i => i.Label));
                    if(bestMatch != null)
                    {
                        Console.WriteLine("VIAF Candidates:");
                        idsAndLabels.ForEach(Console.WriteLine);
                        Console.WriteLine($"Best: {bestMatch.Value}; Score: {bestMatch.Score}");
                        var bestIdAndLabel = idsAndLabels[bestMatch.Index];
                        authority.Viaf = bestIdAndLabel.Identifier;
                    }
                }

                var ulans = laObj.Equivalent.Where(e => e.Id.StartsWith(Authority.UlanPrefix)).ToList();
                if (ulans.Count == 1)
                {
                    authority.Ulan = ulans[0].Id.Replace(Authority.UlanPrefix, "");
                }
                else if (ulans.Count > 1)
                {
                    Console.WriteLine($"{ulans.Count} Multiple ULAN equivalents");
                    var actors = new List<Actor>();
                    foreach(var u in ulans)
                    {
                        var actor = await ulanClient.GetFromIdentifier(u.Id.Replace(Authority.UlanPrefix, ""));
                        actors.Add(actor);
                    }
                    var bestMatch = FuzzySharp.Process.ExtractOne(disambiguator, actors.Select(a => a.Label));
                    Console.WriteLine("ULAN Candidates:");
                    actors.ForEach(a => Console.WriteLine(a.Label));
                    Console.WriteLine($"Best: {bestMatch.Value}; Score: {bestMatch.Score}");
                    var bestActor = actors[bestMatch.Index];
                    authority.Ulan = bestActor.Id.Replace(Authority.UlanPrefix, "");
                }

                var wkds = laObj.Equivalent.Where(e => e.Id.StartsWith(Authority.WikidataPrefix)).ToList();
                if (wkds.Count == 1)
                {
                    authority.Wikidata = wkds[0].Id.Replace(Authority.WikidataPrefix, "");
                }
                else if (wkds.Count > 1)
                {
                    Console.WriteLine($"{wkds.Count} Multiple Wikidata equivalents");
                    var idsAndLabels = new List<IdentifierAndLabel>();
                    foreach (var w in wkds)
                    {
                        var idAndLabel = await wikidataClient.GetName(w.Id.Replace(Authority.WikidataPrefix, ""));
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



        public async Task<Dictionary<string, Authority>> AddCandidatesFromLoc(string authorityType, string? term, string? variant = null)
        {
            Func<string, Task<List<IdentifierAndLabel>>> locSuggester;
            Func<string, Task<List<IdentifierAndLabel>>>? alternateSuggester = null;
            bool isName = false;
            switch(authorityType)
            {
                case "Person":
                case "Group":
                    locSuggester = locClient.SuggestName;
                    isName = true;
                    break;
                case "Place":
                    locSuggester = locClient.SuggestPlace;
                    break;
                case "Concept":
                    locSuggester = locClient.SuggestHeading;
                    alternateSuggester = locClient.SuggestName;
                    break;
                    default: 
                    throw new ArgumentException("Unsupported authority type", nameof(authorityType));
            }
            var candidateAuthorities = new Dictionary<string, Authority>();
            if(string.IsNullOrWhiteSpace(term))
            {
                return candidateAuthorities;
            }
            var locResults = await locSuggester(term);
            var firstLoc = locResults.FirstOrDefault();
            if (firstLoc == null && variant != null)
            {
                locResults = await locSuggester(variant);
                firstLoc = locResults.FirstOrDefault();
            }
            if (firstLoc == null && isName && term.IndexOf(" and ") > 0)
            {
                locResults = await locSuggester(term.Replace(" and ", " & "));
                firstLoc = locResults.FirstOrDefault();
            }

            string MainPart(string locIdentifier)
            {
                // e.g., n82068148-781 in https://id.loc.gov/authorities/names/n82068148-781.html
                return locIdentifier.Split('-').First();
            }

            if (firstLoc != null)
            {
                var authority = new Authority { Loc = MainPart(firstLoc.Identifier), Label = firstLoc.Label };
                candidateAuthorities["loc1"] = authority;
            }
            if (alternateSuggester != null)
            {
                var altLocResults = await alternateSuggester(term);
                var firstAltLoc = altLocResults.FirstOrDefault();
                if (firstAltLoc != null)
                {
                    var authority = new Authority { Loc = MainPart(firstAltLoc.Identifier), Label = firstAltLoc.Label };
                    candidateAuthorities["loc2"] = authority;
                }
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

        public async Task<Dictionary<string, Authority>> AddCandidatesFromUlan(
            string authorityType, string? agentString, string? variant = null)
        {
            var candidateAuthorities = new Dictionary<string, Authority>();
            if(string.IsNullOrWhiteSpace(agentString))
            {
                return candidateAuthorities;
            }
            // ulans
            // try a simple match first
            const int requiredPercentageMatch = 94; // This is a percentage of chars in the word
            var ulansLevAll = await ulanClient.GetIdentifiersAndLabelsLevenshtein(agentString, authorityType, 5);
            var ulansLevPassing = ulansLevAll
                .Where(x => x.Score >= requiredPercentageMatch)
                .ToList();
            if (ulansLevPassing.Count == 0 && variant != null)
            {
                ulansLevAll = await ulanClient.GetIdentifiersAndLabelsLevenshtein(variant, authorityType, 5);
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
                    Score = ulanMatch.Score,
                    Type = authorityType
                };
                candidateAuthorities[$"ulan{ulanCounter++}"] = authority;
            }
            return candidateAuthorities;
        }


        public async Task<Dictionary<string, Authority>> AddLookupCandidatesFromLux(string authorityType, string name)
        {
            // OK here need to do different types of search and merge luxes together
            var candidateAuthorities = new Dictionary<string, Authority>();
            if (string.IsNullOrWhiteSpace(name))
            {
                return candidateAuthorities;
            }
            int luxCounter = 1;
            List<LinkedArtObject> linkedArtObjects;
            switch(authorityType)
            {
                case "Person":
                case "Group":
                    var agents = await luxClient.AgentSearch(name);
                    linkedArtObjects = agents.Cast<LinkedArtObject>().ToList();
                    break;
                case "Place":
                    var places = await luxClient.PlaceSearch(name);
                    linkedArtObjects = places.Cast<LinkedArtObject>().ToList();
                    break;
                case "Concept":
                    var concepts = await luxClient.ConceptSearch(name);
                    var agents2 = await luxClient.AgentSearch(name);
                    var places2 = await luxClient.PlaceSearch(name);
                    linkedArtObjects = concepts.Cast<LinkedArtObject>().ToList();
                    linkedArtObjects.AddRange(agents2.Cast<LinkedArtObject>());
                    linkedArtObjects.AddRange(places2.Cast<LinkedArtObject>());
                    break;
                default:
                    throw new ArgumentException("Unsupported authority type", nameof(authorityType));
            }
            foreach (var laObj in linkedArtObjects)
            {
                var authority = await GetFromEquivalents(laObj, name);
                authority.Score = 100; // how to tell...
                candidateAuthorities[$"lux{luxCounter++}"] = authority;
            }
            return candidateAuthorities;
        }

        public async Task<Dictionary<string, Authority>> AddWorkByCandidatesFromLux(
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




        public Authority? DecideBestCandidate(
            List<string> workIds, string term, 
            Dictionary<string, Authority> candidateAuthorities,
            string expectedAuthorityType,
            int minBestMatchScore = 3)
        {
            if (candidateAuthorities.Count == 0)
            {
                return null;
            }

            Authority bestCandidate = new();

            var luxMatches = ExtractAuthoritiesBySource("lux", candidateAuthorities);
            var ulanMatches = ExtractAuthoritiesBySource("ulan", candidateAuthorities);
            var viafMatches = ExtractAuthoritiesBySource("viaf", candidateAuthorities);
            var locMatches = ExtractAuthoritiesBySource("loc", candidateAuthorities);

            var nonLuxMatches = new List<Authority>(viafMatches);
            nonLuxMatches.AddRange(ulanMatches);
            nonLuxMatches.AddRange(locMatches);

            // we only want one locMatch
            Authority? locMatch = null;
            if(locMatches.Count == 1)
            {
                locMatch = locMatches[0];
            }
            else if(locMatches.Count > 1)
            {
                var closestLabel = FuzzySharp.Process.ExtractOne(term, locMatches.Select(a => a.Label));
                locMatch = locMatches.First(a => a.Label == closestLabel.Value);
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

            if (luxMatches.Count == 1 && HasAtLeastOneEquivalentFromLookups(luxMatches[0]))
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

                if (bestCandidate.Score >= minBestMatchScore)
                {
                    bestCandidate.Lux = luxMatch.Lux;
                    if(string.IsNullOrWhiteSpace(bestCandidate.Type))
                    {
                        if(luxMatch != null)
                        {
                            bestCandidate.Type = luxMatch.Type;
                        }
                        else if(expectedAuthorityType != "Concept")
                        {
                            bestCandidate.Type = expectedAuthorityType;
                        }
                        else
                        {
                            foreach(var match in nonLuxMatches.OrderByDescending(a => a.Score))
                            {
                                if(match.Type.HasText())
                                {
                                    bestCandidate.Type = match.Type;
                                    break;
                                }
                            }
                        }
                    }
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

        public bool HasAtLeastOneEquivalentFromLookups(Authority authority)
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
