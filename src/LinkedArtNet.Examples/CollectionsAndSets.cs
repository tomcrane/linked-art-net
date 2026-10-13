

using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples
{
    public class CollectionsAndSets
    {

        [LinkedArtExample("set/exhset/1")]
        [SiteIssue("The site publishes two examples as set/exhset/1; the JSON at that URL is the exhibition page's version, which classifies the set as an Exhibition Collection, not this unclassified one.")]
        public static Set Exhibition_Objects()
        {
            // This is nearly the same as the example in Exhibitions with the same name
            var set = new Set()
                .WithContext()
                .WithId("https://linked.art/example/set/exhset/1")
                .WithLabel("Exhibition objects");

            set.IdentifiedBy = [new Name("Objects in Manet and Modern Beauty").AsPrimaryName()];

            set.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Aat.Description, Aat.Statement)
                    .WithContent("Objects in the exhibition Manet and Modern Beauty at the Art Institute of Chicago and the Getty Museum")
            ];

            set.CreatedBy = new Creation()
            {
                TimeSpan = LinkedArtTimeSpan.FromDay(2019,5,1)
            };

            return set;
        }

        [LinkedArtExample("object/spring/13")]
        public static HumanMadeObject Spring_in_Set()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/spring/13")
                .WithLabel("Jeanne (Spring) by Manet")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

            spring.IdentifiedBy = [new Name("Jeanne (Spring)")];

            spring.MemberOf = [
                new Set()
                    .WithId("https://linked.art/example/set/exhset")
            ];

            return spring;
        }

        [LinkedArtExample("object/letter/2")]
        public static HumanMadeObject Order_of_Members()
        {
            // This has been copied from the Archives doc examples
            var parent = new Set()
                .WithId("https://linked.art/example/set/archive_sfl")
                .WithLabel("Stieglitz Family Letters");

            var obermeyer = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/letter/2")
                .WithLabel("Obermeyer 1920");

            obermeyer.IdentifiedBy = [
                new Name("Obermeyer, Bertha (1920)").AsPrimaryName(),
                Identifier.SortValue("000001", parent)
            ];

            obermeyer.MemberOf = [parent];

            return obermeyer;
        }

        [LinkedArtExample("set/exhset/2")]
        public static Set Prototypical_Members()
        {
            var set = new Set()
                .WithContext()
                .WithId("https://linked.art/example/set/exhset/2")
                .WithLabel("Exhibition objects");

            var paintingByManet = new HumanMadeObject()
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);
            paintingByManet.ProducedBy = new Production()
            {
                CarriedOutBy = [
                    Ulan.Person("500010363", "Manet")
                ]
            };
            set.MembersExemplifiedBy = [paintingByManet];

            return set;
        }


        [LinkedArtExample("set/rijks_objects/1")]
        public static Set Rijksmuseum_Collection()
        {
            var set = new Set()
                .WithContext()
                .WithId("https://linked.art/example/set/rijks_objects/1")
                .WithLabel("Collection of the Rijksmuseum")
                .WithClassifiedAs(Aat.Collection);

            set.IdentifiedBy = [new Name("Collection of the Rijksmuseum").AsPrimaryName()];

            return set;
        }


        [LinkedArtExample("set/rijks_paintings/1")]
        public static Set Rijksmuseum_Paintings()
        {
            var set = new Set()
                .WithContext()
                .WithId("https://linked.art/example/set/rijks_paintings/1")
                .WithLabel("Paintings of the Rijksmuseum")
                .WithClassifiedAs(Aat.Collection);

            set.IdentifiedBy = [new Name("Paintings of the Rijksmuseum").AsPrimaryName()];
            set.MemberOf = [
                new Set()
                    .WithId("https://linked.art/example/set/rijks_objects")
                    .WithLabel("Collection of the Rijksmuseum")
            ];
            set.UsedFor = [
                new Activity()
                {
                    ClassifiedAs = [Aat.Curating],
                    CarriedOutBy = [
                        new Group()
                            .WithId("https://linked.art/example/group/rijks_paintings_dept")
                            .WithLabel("Paintings Department")
                    ]
                }
            ];

            return set;
        }


        [LinkedArtExample("object/nightwatch/16")]
        public static HumanMadeObject Nightwatch_in_Paintings()
        {
            var nightwatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/16")
                .WithLabel("Night Watch by Rembrandt")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

            nightwatch.IdentifiedBy = [new Name("The Night Watch").AsPrimaryName()];

            nightwatch.MemberOf = [
                new Set()
                    .WithId("https://linked.art/example/set/rijks_paintings")
                    .WithLabel("Paintings of the Rijksmuseum")
            ];

            return nightwatch;
        }

        [LinkedArtExample("group/14thc_italian/1")]
        public static Group Unidentified_14th_Century_Italian()
        {
            var group = new Group()
                .WithContext()
                .WithId("https://linked.art/example/group/14thc_italian/1")
                .WithLabel("14th c italians");

            group.IdentifiedBy = [new Name("Unidentified 14th Century Italian").AsPrimaryName()];

            group.MembersExemplifiedBy = [
                new Person
                {
                    Born = new Birth
                    {
                        TimeSpan = new LinkedArtTimeSpan
                        {
                            BeginOfTheBegin = new LinkedArtDate(1300, 1, 1),
                            EndOfTheEnd = new LinkedArtDate(1399, 12, 31)
                        },
                        TookPlaceAt = [
                            new Place()
                                .WithId("https://vocab.getty.edu/tgn/1000080")
                                .WithLabel("Italy")
                        ]
                    }
                }
            ];

            return group;
        }

    }
}
