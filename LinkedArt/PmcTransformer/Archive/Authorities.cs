using LinkedArtNet;
using LinkedArtNet.Parsers;
using LinkedArtNet.Vocabulary;
using System.Xml.Linq;

namespace PmcTransformer.Archive
{
    public static class Authorities
    {
        static TimespanParser timespanParser = new TimespanParser();

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
                        actor = new Group().WithClassifiedAs(Getty.Family);
                        break;
                    case "Corporate":
                        actor = new Group().WithClassifiedAs(Getty.Organization);
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
                if(surname != null)
                {
                    name.Part = [ new Name(surname).WithClassifiedAs(Getty.FamilyName) ];
                }
                if(forenames != null)
                {
                    name.Part ??= [];
                    name.Part.Add( new Name(forenames).WithClassifiedAs(Getty.GivenName) );
                }
                var dates = record.ArcStrings("Dates").SingleOrDefault();
                if(dates != null)
                {
                    var parsed = timespanParser.ParseSimpleYearDateRange(dates);
                    if(parsed != null)
                    {
                        if (parsed.Item3.IsDatesActive)
                        {
                            var professionalActivities = new Activity()
                                .WithClassifiedAs(Getty.AatType("Professional Activities", "300393177"));
                            professionalActivities.TimeSpan = new LinkedArtTimeSpan()
                            {
                                BeginOfTheBegin = parsed.Item1?.BeginOfTheBegin,
                                EndOfTheEnd = parsed.Item2?.EndOfTheEnd,
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
                                if(parsed.Item2 != null)
                                {
                                    p.Died = new Activity(Types.Death)
                                    {
                                        TimeSpan = parsed.Item2
                                    };

                                }
                            }
                        }
                    }
                }

            }
    }
}
