

using LinkedArtNet.Vocabulary;
using LinkedArtNet;

namespace Examples.NewDocExamples
{
    public class CollectionsAndSets
    {
        public static void Create()
        {
            Exhibition_Objects();
            Spring_in_Set();
            Order_of_Members();
            Prototypical_Members();
            Rijksmuseum_Collection();
            Rijksmuseum_Paintings();
            Nightwatch_in_Paintings();
        }


        private static void Exhibition_Objects()
        {
            // This is nearly the same as the example in Exhibitions with the same name
            var set = new Set()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/set/exhset/1")
                .WithLabel("Exhibition objects");

            set.IdentifiedBy = [new Name("Objects in Manet and Modern Beauty").AsPrimaryName()];

            set.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Aat.Description, Aat.BriefText)
                    .WithContent("Objects in the exhibition Manet and Modern Beauty at the Art Institute of Chicago and the Getty Museum")
            ];

            set.CreatedBy = new Creation()
            {
                TimeSpan = LinkedArtTimeSpan.FromDay(2019,5,1)
            };

            Documentation.Save(set, false);
        }

        private static void Spring_in_Set()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/spring/13")
                .WithLabel("Jeanne (Spring) by Manet")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

            spring.IdentifiedBy = [new Name("Jeanne (Spring)")];

            spring.MemberOf = [
                new Set()
                    .WithId($"{Documentation.IdRoot}/set/exhset")
            ];

            Documentation.Save (spring);
        }

        private static void Order_of_Members()
        {
            // This has been copied from the Archives doc examples
            var parent = new Set()
                .WithId($"{Documentation.IdRoot}/set/archive_sfl")
                .WithLabel("Stieglitz Family Letters");

            var obermeyer = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/letter/2")
                .WithLabel("Obermeyer 1920");

            obermeyer.IdentifiedBy = [
                new Name("Obermeyer, Bertha (1920)").AsPrimaryName(),
                Identifier.SortValue("000001", parent)
            ];

            obermeyer.MemberOf = [parent];

            Documentation.Save(obermeyer);
        }

        private static void Prototypical_Members()
        {
            var set = new Set()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/set/exhset/2")
                .WithLabel("Exhibition objects");

            var paintingByManet = new HumanMadeObject()
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);
            paintingByManet.ProducedBy = new Production()
            {
                CarriedOutBy = [
                    Ulan.Manet
                ]
            };
            set.MembersExemplifiedBy = [paintingByManet];

            Documentation.Save(set);
        }


        private static void Rijksmuseum_Collection()
        {
            var set = new Set()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/set/rijks_objects/1")
                .WithLabel("Collection of the Rijksmuseum")
                .WithClassifiedAs(Aat.Collection);

            set.IdentifiedBy = [new Name("Collection of the Rijksmuseum").AsPrimaryName()];

            Documentation.Save(set);
        }


        private static void Rijksmuseum_Paintings()
        {
            var set = new Set()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/set/rijks_paintings/1")
                .WithLabel("Paintings of the Rijksmuseum")
                .WithClassifiedAs(Aat.Collection);

            set.IdentifiedBy = [new Name("Paintings of the Rijksmuseum").AsPrimaryName()];
            set.MemberOf = [
                new Set()
                    .WithId($"{Documentation.IdRoot}/set/rijks_objects")
                    .WithLabel("Collection of the Rijksmuseum")
            ];
            set.UsedFor = [
                new Activity()
                {
                    ClassifiedAs = [Aat.Curating],
                    CarriedOutBy = [
                        new Group()
                            .WithId($"{Documentation.IdRoot}/group/rijks_paintings_dept")
                            .WithLabel("Paintings Department")
                    ]
                }
            ];

            Documentation.Save(set);
        }


        private static void Nightwatch_in_Paintings()
        {
            var nightwatch = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/nightwatch/16")
                .WithLabel("Night Watch by Rembrandt")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

            nightwatch.IdentifiedBy = [new Name("The Night Watch").AsPrimaryName()];

            nightwatch.MemberOf = [
                new Set()
                    .WithId($"{Documentation.IdRoot}/set/rijks_paintings")
                    .WithLabel("Paintings of the Rijksmuseum")
            ];

            Documentation.Save (nightwatch);
        }
    }
}
