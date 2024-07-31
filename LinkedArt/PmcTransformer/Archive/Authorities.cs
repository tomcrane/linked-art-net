using LinkedArtNet;
using LinkedArtNet.Parsers;
using LinkedArtNet.Vocabulary;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PmcTransformer.Archive
{
    public static partial class AuthorityParser
    {
        static readonly TimespanParser timespanParser = new();

        public static Dictionary<string, Actor> CreateArchiveAuthorityDict(XDocument xAuthorities)
        {
            var dict = new Dictionary<string, Actor>();
            foreach (var record in xAuthorities.Root!.Elements())
            {
                Actor actor;
                var recordType = record.ArcStrings("RecordType").Single();
                switch (recordType)
                {
                    case "Person":
                        actor = new Person();
                        break;
                    case "Family":
                        actor = new LinkedArtNet.Group().WithClassifiedAs(Getty.Family);
                        break;
                    case "Corporate":
                        actor = new LinkedArtNet.Group().WithClassifiedAs(Getty.Organization);
                        break;
                    default:
                        throw new NotImplementedException("Unknown recordType");
                }
                var recordId = record.ArcStrings("RecordID").Single();  // GUID
                var code = record.ArcStrings("Code").Single();          // DS/UK/nnn
                actor.WithId($"{Identity.ArchiveAuthority}{recordId}");
                var personName = record.ArcStrings("PersonName").Single();
                var name = new Name(personName).AsPrimaryName();
                actor.IdentifiedBy = [
                    new Identifier(code).AsSystemAssignedNumber(),
                    name
                ];
                dict[code] = actor;  // use the code rather than recordId, as code is used in descriptions
                
                var surname = record.ArcStrings("Surname").SingleOrDefault();
                var forenames = record.ArcStrings("Forenames").SingleOrDefault();
                if (surname != null)
                {
                    name.Part = [new Name(surname).WithClassifiedAs(Getty.FamilyName)];
                }
                if (forenames != null)
                {
                    name.Part ??= [];
                    name.Part.Add(new Name(forenames).WithClassifiedAs(Getty.GivenName));
                }
                
                var dates = record.ArcStrings("Dates").SingleOrDefault();
                if (dates != null)
                {
                    AssignDates(actor, dates);
                }

                foreach(var parallelEntry in record.ArcStrings("ParallelEntry"))
                {
                    actor.IdentifiedBy.Add(new Name(parallelEntry));
                }

                var nonPreferredTerm = record.ArcStrings("NonPreferredTerm").SingleOrDefault();
                if (nonPreferredTerm != null)
                {
                    actor.IdentifiedBy.Add(new Name(nonPreferredTerm));
                }

                Helpers.SimpleStatement(record, actor, "Nationality", Getty.Description);
                Helpers.SimpleStatement(record, actor, "Activity", Getty.Description);
                Helpers.SimpleStatement(record, actor, "Relationships", Getty.Description);

                var textEquiv = record.ArcStrings("Text").SingleOrDefault();
                var setEquiv = record.ArcStrings("Set").SingleOrDefault();

                LookForEquivalents(actor, textEquiv);
                LookForEquivalents(actor, setEquiv);

                Writer.WriteToDisk(actor);

            }

            return dict;
        }


        [GeneratedRegex(@"VIAF: ([\d]*)")]
        private static partial Regex Viaf();

        [GeneratedRegex(@"ULAN: ([\d]*)")]
        private static partial Regex Ulan();

        private static void LookForEquivalents(Actor actor, string? s)
        {
            if(string.IsNullOrWhiteSpace(s))
            { 
                return;
            }

            var viaf = Viaf().Match(s)?.Groups.Values.Skip(1).SingleOrDefault();
            if(viaf != null)
            {
                actor.Equivalent = [
                    actor.GetReferenceObject(false)
                        .WithId("http://viaf.org/viaf/" + viaf)
                ];
                return;
            } 

            var ulan = Ulan().Match(s)?.Groups.Values.Skip(1).SingleOrDefault();
            if (ulan != null)
            {
                actor.Equivalent = [
                    actor.GetReferenceObject(false)
                        .WithId("http://vocab.getty.edu/ulan/" + ulan)
                ];
            }
        }

        private static void AssignDates(Actor actor, string dates)
        {
            // We call this rather than the more general parse because we know what the source data looks like.
            // But eventually we shouldn't have to know.
            var parsed = timespanParser.ParseSimpleYearDateRange(dates);
            if (parsed != null)
            {
                if (parsed.Item3.IsDatesActive)
                {
                    var professionalActivities = new Activity()
                        .WithClassifiedAs(Getty.AatType("Professional Activities", "300393177"));
                    professionalActivities.TimeSpan = new LinkedArtTimeSpan()
                    {
                        BeginOfTheBegin = parsed.Item1?.BeginOfTheBegin,
                        EndOfTheEnd = parsed.Item2?.EndOfTheEnd, // might be null
                        Label = dates
                    };
                    actor.CarriedOut = [professionalActivities];
                }
                else
                {
                    if (actor is Person p)
                    {
                        p.Born = new Activity(Types.Birth)
                        {
                            TimeSpan = parsed.Item1
                        };
                        if (parsed.Item2 != null)
                        {
                            p.Died = new Activity(Types.Death)
                            {
                                TimeSpan = parsed.Item2
                            };
                        }
                    }
                    else if (actor is LinkedArtNet.Group g)
                    {
                        g.FormedBy = new Activity(Types.Formation)
                        {
                            TimeSpan = parsed.Item1
                        };
                        if (parsed.Item2 != null)
                        {
                            g.DissolvedBy = new Activity(Types.Dissolution)
                            {
                                TimeSpan = parsed.Item2
                            };
                        }
                    }
                    else
                    {
                        throw new Exception("Actor is an unsupported type: " + actor.GetType().Name);
                    }
                }
            }
        }
    }
}
