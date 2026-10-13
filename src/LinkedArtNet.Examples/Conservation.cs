

using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples
{
    public class Conservation
    {

        [LinkedArtExample("object/nightwatch/25")]
        public static HumanMadeObject Nightwatch_Condition()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/25")
                .WithLabel("The Night Watch")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

            nightWatch.AttributedBy = [
                new AttributeAssignment()
                {
                    Label = "Condition Assessment of Night Watch",
                    IdentifiedBy = [new Name("Thorough Condition Assessment")],
                    ReferredToBy = [
                        new LinguisticObject()
                            .WithClassifiedAs(Aat.Description, Aat.Statement)
                            .WithContent("Very fine cracks throughout the painted surface")
                    ],
                    TimeSpan = new LinkedArtTimeSpan
                    {
                        BeginOfTheBegin = new LinkedArtDate(2019, 5, 1),
                        EndOfTheEnd = new LinkedArtDate(2019, 9, 30).LastSecondOfDay()
                    },
                    CarriedOutBy = [
                        new Group()
                            .WithId("https://linked.art/example/group/rijksmuseum")
                            .WithLabel("Rijksmuseum")
                    ],
                    AssignedProperty = "classified_as",
                    Assigned = [ Aat.Term("300387447", "Microcracks") ],
                    PartOf = [
                        new Activity()
                            .WithId("https://linked.art/example/event/operation_nightwatch")
                            .WithLabel("Operation Night Watch")
                    ]
                }
            ];

            return nightWatch;
        }


        [LinkedArtExample("object/nightwatch/26")]
        public static HumanMadeObject Nightwatch_Improvements()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/26")
                .WithLabel("The Night Watch")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

            nightWatch.ModifiedBy = [
                new Modification()
                {
                    Label = "Conservation of Night Watch",
                    ReferredToBy = [
                        new LinguisticObject()
                            .WithClassifiedAs(Aat.Description, Aat.Statement)
                            .WithContent("Minor conservation work in response to earlier survey")
                    ],
                    Technique = [Aat.Term("300053027", "Cleaning")],
                    TimeSpan = new LinkedArtTimeSpan
                    {
                        BeginOfTheBegin = new LinkedArtDate(2020, 1, 1),
                        EndOfTheEnd = new LinkedArtDate(2021, 12, 31).LastSecondOfDay()
                    },
                    CarriedOutBy = [
                        new Group()
                            .WithId("https://linked.art/example/group/rijksmuseum")
                            .WithLabel("Rijksmuseum")
                    ],
                    PartOf = [
                        new Activity()
                            .WithId("https://linked.art/example/event/operation_nightwatch")
                            .WithLabel("Operation Night Watch")
                    ]
                }
            ];

            return nightWatch;
        }


        [LinkedArtExample("event/operation_nightwatch/1")]
        public static Activity Nightwatch_Project()
        {
            var operationNW = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/event/operation_nightwatch/1")
                .WithLabel("Conservation Project")
                .WithClassifiedAs(Aat.ConservationActivity);

            operationNW.IdentifiedBy = [new Name("Operation Night Watch").AsPrimaryName()];
            operationNW.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Aat.Description, Aat.Statement)
                    .WithContent("Operation Night Watch is the biggest and most wide-ranging ever study of Rembrandt’s most famous painting.")
            ];
            operationNW.TimeSpan = new LinkedArtTimeSpan
            {
                BeginOfTheBegin = new LinkedArtDate(2019, 1, 1),
                EndOfTheEnd = new LinkedArtDate(2023, 12, 31).LastSecondOfDay()
            };
            operationNW.CarriedOutBy = [
                new Group()
                    .WithId("https://linked.art/example/group/rijksmuseum")
                    .WithLabel("Rijksmuseum"),
                new Group()
                    .WithId("https://linked.art/example/group/akzonobel")
                    .WithLabel("AkzoNobel")
            ];

            return operationNW;

        }
    }
}
