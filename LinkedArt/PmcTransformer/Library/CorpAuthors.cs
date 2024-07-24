using LinkedArtNet;
using PmcTransformer.Helpers;
using Group = LinkedArtNet.Group;
using Dapper;


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
                var corpAuthorString = corpAuthor.Key.Trim().TrimEnd('.').Trim();
                string? reduced = corpAuthorString.ReduceGroup();
                if (reduced == corpAuthorString) reduced = null;

                counter++;
                string? authorityIdentifier = conn.GetAuthorityIdentifier("corpauthor", corpAuthorString, true);
                if (string.IsNullOrEmpty(authorityIdentifier))
                {
                    // need to find out what this thing is...

                    // try a simple match first
                    var ulans = conn.Query<IdentifierAndLabel>(
                        "select identifier, label from ulan_labels where label=@Label and type='Group'", new { Label = corpAuthorString })
                        .ToList();

                    if(ulans.Count == 0 && reduced != null)
                    {
                        ulans = conn.Query<IdentifierAndLabel>(
                            "select identifier, label from ulan_labels where label=@Label and type='Group'", new { Label = reduced })
                            .ToList();
                        if(ulans.Count != 0)
                        {
                            Console.WriteLine();
                        }
                    }

                    if(ulans.Count > 1)
                    {
                        Console.WriteLine("Warning - more than one ULAN label match for " + corpAuthorString);
                    }
                    
                    if(ulans.Count == 1)
                    {
                        matches++;
                        var ulanIdAndLabel = new IdentifierAndLabel { Identifier = ulans[0].Identifier, Label = ulans[0].Label };
                        conn.UpsertAuthority("corpauthor", corpAuthorString, "ulan", "Group", ulanIdAndLabel);
                    }
                    else
                    {
                        // try different forms of the name against ULAN
                        // TODO

                        var viafAuthority = ViafClient.SearchBestMatch("local.corporateNames all ", corpAuthorString);
                        if(viafAuthority != null)
                        {
                            matches++;
                            conn.UpsertAuthority("corpauthor", corpAuthorString, "Group", viafAuthority);
                        }
                        else
                        {
                            // try Library of Congress suggest
                            // replace this with a local query service in future
                            var locResults = LocClient.SuggestName(corpAuthorString);
                            // first pass, just take the first result
                            var firstLoc = locResults.FirstOrDefault();
                            if (firstLoc == null && reduced != null)
                            {
                                locResults = LocClient.SuggestName(reduced);
                                firstLoc = locResults.FirstOrDefault();
                            }

                            if (firstLoc != null)
                            {
                                matches++;
                                conn.UpsertAuthority("corpauthor", corpAuthorString, "loc", "Group", firstLoc);
                            }
                        }
                    }
                }
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
                    string? authorityIdentifier = conn.GetAuthorityIdentifier("corpauthor", corpAuthorString, false);
                    if (authorityIdentifier != null)
                    {
                        var authority = conn.QueryFirstOrDefault<Authority>(
                            "select * from authorities where identifier=@Identifier",
                            new { Identifier = authorityIdentifier });
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
