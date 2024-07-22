using LinkedArtNet;
using PmcTransformer.Helpers;
using Group = LinkedArtNet.Group;
using Dapper;
using Npgsql;


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
                var source = corpAuthor.Key.Trim().TrimEnd('.');
                counter++;
                string? authorityIdentifier = conn.GetAuthorityIdentifier("group_source_map", source, createEntryForSource:true);
                if (string.IsNullOrEmpty(authorityIdentifier))
                {
                    // need to find out what this thing is...

                    // try a simple match first
                    var ulans = conn.Query<IdentifierAndLabel>(
                        "select identifier, label from ulan_labels where label=@Label and type='Group'", new { Label = source })
                        .ToList();

                    if(ulans.Count > 1)
                    {
                        Console.WriteLine("Warning - more than one ULAN label match for " + source);
                    }
                    else if(ulans.Count == 1)
                    {
                        matches++;
                        var authority = conn.QuerySingleOrDefault<Authority>(
                            "select * from authorities where ulan=@Ulan", new { Ulan = ulans[0].Identifier });
                        // this only works if we ONLY use ULANs
                        if(authority == null)
                        {
                            var newId = IdMinter.Generate();
                            conn.Execute("insert into authorities (identifier, type, ulan, label) " +
                                         "values (@Identifier, 'Group', @Ulan, @Label)",
                                         new { Identifier = newId, Ulan = ulans[0].Identifier, Label = ulans[0].Label });
                            authority = conn.QuerySingleOrDefault<Authority>(
                                "select * from authorities where ulan=@Ulan", new { Ulan = ulans[0].Identifier });
                        }
                        // authority is not null here
                        conn.Execute("update group_source_map set authority=@Authority where source_string=@Source",
                            new { Authority = authority!.Identifier, Source = source });

                    }
                    else
                    {
                        // try different forms of the name against ULAN

                        // try LOC next
                        // https://id.loc.gov/authorities/names/suggest/?q=Hartnoll%20&%20Eyre

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
                var source = corpAuthor.Key.Trim().TrimEnd('.');
                Group? groupRef = null;
                if(source.StartsWith(Locations.PhotoArchiveName))
                {
                    groupRef = Locations.PhotoArchiveGroupRef;
                }
                else
                {
                    string? authorityIdentifier = conn.GetAuthorityIdentifier("group_source_map", source);
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
                            .WithLabel(source);
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
