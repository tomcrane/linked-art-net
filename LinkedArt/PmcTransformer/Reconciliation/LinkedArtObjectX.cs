using LinkedArtNet;

namespace PmcTransformer.Reconciliation
{
    public static class LinkedArtObjectX
    {
        public static Authority AuthorityFromEquivalents(this LinkedArtObject laObj, string? disambiguator = null)
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
                if(locs.Count == 1)
                {
                    authority.Loc = locs[0].Id.Replace(locPrefix, "");
                }
                else if(locs.Count > 1)
                {
                    Console.WriteLine($"{locs.Count} Multiple LOC equivalents");
                    var idsAndLabels = locs.Select(l => LocClient.GetName(l.Id.Replace(locPrefix, ""))).ToList();
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
                    var idsAndLabels = viafs.Select(l => ViafClient.GetName(l.Id.Replace(viafPrefix, ""))).ToList();
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
                    var actors = ulans.Select(l => UlanClient.GetFromIdentifier(l.Id.Replace(ulanPrefix, ""))).ToList();
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
                    var idsAndLabels = wkds.Select(l => WikidataClient.GetName(l.Id.Replace(wikiPrefix, ""))).ToList();
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
    }
}
