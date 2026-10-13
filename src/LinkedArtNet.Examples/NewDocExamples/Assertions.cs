

using LinkedArtNet;
using LinkedArtNet.Vocabulary;

namespace Examples.NewDocExamples
{
    public class Assertions
    {
        public static void Create()
        {
            Assertion_that_Spring_Is_Canvas();
            Two_Accession_Numbers();
            Source_of_Knowledge();
            Uncertain_Attribution();
            // Context_Specific_Assignment(); Needs fixing on site
            Non_Specific_Relationship();
            Bol_Student_of_Rembrandt();
        }


        private static void Assertion_that_Spring_Is_Canvas()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/spring/21")
                .WithLabel("Spring")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

            spring.AttributedBy = [
                new AttributeAssignment()
                {
                    TimeSpan = LinkedArtTimeSpan.FromYear(2015),
                    AssignedProperty = "made_of",
                    Assigned = [
                        new Material { Id = Aat.Uri("300014078") }
                    ]
                }
            ];

            // remove label from year to match docs

            Documentation.Save( spring );
        }


        private static void Two_Accession_Numbers()
        {
            var portrait = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/yiadom-boakye/1")
                .WithLabel("Portrait of Lynette Yiadom-Boakye")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

            var yuagId = new Identifier("2021.25.1").AsAccessionNumber();
            yuagId.AssignedBy = [
                new AttributeAssignment()
                {
                    CarriedOutBy = [
                        new Group()
                            .WithId($"{Documentation.IdRoot}/group/yuag")
                            .WithLabel("Yale University Art Gallery")
                            .WithClassifiedAs(Aat.Museum)
                    ]
                }
            ];

            var ycbaId = new Identifier("B2021.5").AsAccessionNumber();
            ycbaId.AssignedBy = [
                new AttributeAssignment()
                {
                    CarriedOutBy = [
                        new Group()
                            .WithId($"{Documentation.IdRoot}/group/ycba")
                            .WithLabel("Yale Center for British Art")
                            .WithClassifiedAs(Aat.Museum)
                    ]
                }
            ];
            portrait.IdentifiedBy = [ yuagId, ycbaId ];

            Documentation.Save(portrait );
        }


        private static void Source_of_Knowledge()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/person/rembrandt/10")
                .WithLabel("Rembrandt");

            rembrandt.IdentifiedBy = [
                new Name("Rembrandt van Rijn")
                {
                    AssignedBy = [
                        new AttributeAssignment()
                        {
                            UsedSpecificObject = [
                                new LinguisticObject()
                                    .WithId($"{Documentation.IdRoot}/text/gardner-art")
                                    .WithLabel("Art through the Ages")
                            ]
                        }
                    ]
                }
            ];

            Documentation.Save(rembrandt);
        }


        private static void Uncertain_Attribution()
        {
            var forum = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/forum/1")
                .WithLabel("Forum Romanum")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

            forum.IdentifiedBy = [new Name("Forum Romanum").AsPrimaryName()];

            forum.ProducedBy = new Production()
            {
                AttributedBy = [
                    new AttributeAssignment()
                    {
                        ClassifiedAs = [ Aat.PossiblyBy ],
                        Assigned = [
                            new Production()
                            {
                                CarriedOutBy = [
                                    new Person()
                                        .WithId($"{Documentation.IdRoot}/person/corrodi")
                                        .WithLabel("Salomon Corrodi")

                                ]
                            }
                        ],
                        AssignedProperty = "part"
                    }
                ]
            };

            Documentation.Save(forum);              
        }


        private static void Context_Specific_Assignment()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/spring/31")
                .WithLabel("Spring")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

            spring.IdentifiedBy = [new Name("Jeanne (Spring)").AsPrimaryName()];

            spring.AttributedBy = [
                new AttributeAssignment()
                {
                    CarriedOutBy = [
                        new Group()
                            .WithId($"{Documentation.IdRoot}/group/nga")
                            .WithLabel("National Gallery of Art")
                    ],
                    Assigned = [
                        new Identifier("2497-12")
                            .WithClassifiedAs(Aat.EntryNumbers)
                    ],
                    AssignedProperty = "identified_by",
                    CausedBy = [
                        new Activity()
                            .WithId($"{Documentation.IdRoot}/event/post_impressionism")
                            .WithLabel("Post-Impressionism Exhibition")
                    ]
                }
            ];

            Documentation.Save(spring);
        }


        private static void Non_Specific_Relationship()
        {
            var nightwatch = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/nightwatch/17")
                .WithLabel("The Night Watch")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

            nightwatch.IdentifiedBy = [new Name("The Night Watch").AsPrimaryName()];

            nightwatch.AttributedBy = [
                new AttributeAssignment()
                {
                    IdentifiedBy = [
                        new Name("Related Object")
                            .WithClassifiedAs(Aat.DisplayTitle)
                    ],
                    Assigned = [
                        new HumanMadeObject()
                            .WithId($"{Documentation.IdRoot}/object/rppob-28-106")
                            .WithLabel("Nachtwacht")
                    ],
                }
            ];

            // example mismatch
            Documentation.Save(nightwatch, false);
        }


        private static void Bol_Student_of_Rembrandt()
        {
            var bol = new Person()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/person/bol/1")
                .WithLabel("Ferdinand Bol");

            bol.IdentifiedBy = [new Name("Ferdinand Bol").AsPrimaryName()];

            bol.AttributedBy = [
                new AttributeAssignment()
                {
                    IdentifiedBy = [new Name("Student Of").WithClassifiedAs(Aat.DisplayTitle)],
                    Assigned = [ 
                        new Person()
                            .WithId($"{Documentation.IdRoot}/person/rembrandt")
                            .WithLabel("Rembrandt")
                    ]
                }
            ];

            Documentation.Save(bol);
        }
    }
}
