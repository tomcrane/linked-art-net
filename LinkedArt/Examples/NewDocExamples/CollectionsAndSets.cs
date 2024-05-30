

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
            Prototypical_Members();
            Rijksmuseum_Collection();
            Rijksmuseum_Paintings();
            Nightwatch_in_Paintings();
        }


        private static void Exhibition_Objects()
        {
            // This is nearly the same as the example in Exhibitions with the same name
            var set = new LinkedArtObject(Types.Set)
                .WithContext()
                .WithId($"{Documentation.IdRoot}/set/exhset/1")
                .WithLabel("Exhibition objects");

            set.IdentifiedBy = [new Name("Objects in Manet and Modern Beauty").AsPrimaryName()];

            set.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Getty.Description, Getty.BriefText)
                    .WithContent("Objects in the exhibition Manet and Modern Beauty at the Art Institute of Chicago and the Getty Museum")
            ];

            set.CreatedBy = new Activity(Types.Creation)
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
                .WithClassifiedAs(Getty.Painting, Getty.TypeOfWork);

            spring.IdentifiedBy = [new Name("Jeanne (Spring)")];

            spring.MemberOf = [
                new LinkedArtObject(Types.Set)
                    .WithId($"{Documentation.IdRoot}/set/exhset")
            ];

            Documentation.Save (spring);
        }


        private static void Prototypical_Members()
        {
            var set = new LinkedArtObject(Types.Set)
                .WithContext()
                .WithId($"{Documentation.IdRoot}/set/exhset/2")
                .WithLabel("Exhibition objects");

            var paintingByManet = new HumanMadeObject()
                .WithClassifiedAs(Getty.Painting, Getty.TypeOfWork);
            paintingByManet.ProducedBy = new Activity(Types.Production)
            {
                CarriedOutBy = [
                    new Person()
                        .WithId("http://vocab.getty.edu/ulan/500010363")
                        .WithLabel("Manet")
                ]
            };
            set.MembersExemplifiedBy = [paintingByManet];

            Documentation.Save(set);
        }


        private static void Rijksmuseum_Collection()
        {
            var set = new LinkedArtObject(Types.Set)
                .WithContext()
                .WithId($"{Documentation.IdRoot}/set/rijks_objects/1")
                .WithLabel("Collection of the Rijksmuseum")
                .WithClassifiedAs(Getty.Collection);

            set.IdentifiedBy = [new Name("Collection of the Rijksmuseum").AsPrimaryName()];

            Documentation.Save(set);
        }


        private static void Rijksmuseum_Paintings()
        {
            var set = new LinkedArtObject(Types.Set)
                .WithContext()
                .WithId($"{Documentation.IdRoot}/set/rijks_paintings/1")
                .WithLabel("Paintings of the Rijksmuseum")
                .WithClassifiedAs(Getty.Collection);

            set.IdentifiedBy = [new Name("Paintings of the Rijksmuseum").AsPrimaryName()];
            set.MemberOf = [
                new LinkedArtObject(Types.Set)
                    .WithId($"{Documentation.IdRoot}/set/rijks_objects")
                    .WithLabel("Collection of the Rijksmuseum")
            ];
            set.UsedFor = [
                new Activity()
                {
                    ClassifiedAs = [Getty.AatType("Curating", "300054277")],
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
                .WithClassifiedAs(Getty.Painting, Getty.TypeOfWork);

            nightwatch.IdentifiedBy = [new Name("The Night Watch").AsPrimaryName()];

            nightwatch.MemberOf = [
                new LinkedArtObject(Types.Set)
                    .WithId($"{Documentation.IdRoot}/set/rijks_paintings")
                    .WithLabel("Paintings of the Rijksmuseum")
            ];

            Documentation.Save (nightwatch);
        }
    }
}
