using LinkedArtNet;
using PmcTransformer.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Group = LinkedArtNet.Group;

namespace PmcTransformer.Library
{
    public static class CorpAuthors
    {
        public static void ReconcileCorpAuthors(Dictionary<string, LinguisticObject> allWorks, Dictionary<string, List<string>> corpAuthorDict)
        {
            // Create Groups for corpauthor and assert in book record.
            // TODO - this needs to be consistent between runs so once we are sure about our corporation,
            // mint a permanent id for it and store in DB
            int corpIdMinter = 1;
            foreach (var corpAuthor in corpAuthorDict)
            {
                Group? groupRef = null;
                if(corpAuthor.Key.StartsWith(Locations.PhotoArchiveName))
                {
                    groupRef = Locations.PhotoArchiveGroupRef;
                }
                else
                {
                    // map the strings into a local "DB" (dict)
                    // build the dict by reconciling against local postgres of against LUX if no match

                    // save, so that reruns don't re-query


                    // need the full group if we mint/reconcile here
                    groupRef = new Group()
                        .WithId(Identity.GroupBase + corpIdMinter++)
                        .WithLabel(corpAuthor.Key);
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
