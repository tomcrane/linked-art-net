

using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples
{
    public class Assertions
    {

        [LinkedArtExample("object/spring/21")]
        public static HumanMadeObject Assertion_that_Spring_Is_Canvas()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/spring/21")
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

            return spring;
        }


        [LinkedArtExample("object/yiadom-boakye/1")]
        public static HumanMadeObject Two_Accession_Numbers()
        {
            var portrait = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/yiadom-boakye/1")
                .WithLabel("Portrait of Lynette Yiadom-Boakye")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

            var yuagId = new Identifier("2021.25.1").AsAccessionNumber();
            yuagId.AssignedBy = [
                new AttributeAssignment()
                {
                    CarriedOutBy = [
                        new Group()
                            .WithId("https://linked.art/example/group/yuag")
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
                            .WithId("https://linked.art/example/group/ycba")
                            .WithLabel("Yale Center for British Art")
                            .WithClassifiedAs(Aat.Museum)
                    ]
                }
            ];
            portrait.IdentifiedBy = [ yuagId, ycbaId ];

            return portrait;
        }


        [LinkedArtExample("person/rembrandt/10")]
        public static Person Source_of_Knowledge()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId("https://linked.art/example/person/rembrandt/10")
                .WithLabel("Rembrandt");

            rembrandt.IdentifiedBy = [
                new Name("Rembrandt van Rijn")
                {
                    AssignedBy = [
                        new AttributeAssignment()
                        {
                            UsedSpecificObject = [
                                new LinguisticObject()
                                    .WithId("https://linked.art/example/text/gardner-art")
                                    .WithLabel("Art through the Ages")
                            ]
                        }
                    ]
                }
            ];

            return rembrandt;
        }


        [LinkedArtExample("object/forum/1")]
        public static HumanMadeObject Uncertain_Attribution()
        {
            var forum = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/forum/1")
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
                                        .WithId("https://linked.art/example/person/corrodi")
                                        .WithLabel("Salomon Corrodi")

                                ]
                            }
                        ],
                        AssignedProperty = "part"
                    }
                ]
            };

            return forum;
        }


        [LinkedArtExample("object/spring/31")]
        [SiteIssue("The site publishes two examples as object/spring/31; the JSON at that URL is the required-vocabulary page's usage example (no identified_by, local vocab term ids), not this context-specific assignment.")]
        public static HumanMadeObject Context_Specific_Assignment()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/spring/31")
                .WithLabel("Spring")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

            spring.IdentifiedBy = [new Name("Jeanne (Spring)").AsPrimaryName()];

            spring.AttributedBy = [
                new AttributeAssignment()
                {
                    CarriedOutBy = [
                        new Group()
                            .WithId("https://linked.art/example/group/nga")
                            .WithLabel("National Gallery of Art")
                    ],
                    Assigned = [
                        new Identifier("2497-12")
                            .WithClassifiedAs(Aat.EntryNumbers)
                    ],
                    AssignedProperty = "identified_by",
                    CausedBy = [
                        new Activity()
                            .WithId("https://linked.art/example/event/post_impressionism")
                            .WithLabel("Post-Impressionism Exhibition")
                    ]
                }
            ];

            return spring;
        }


        [LinkedArtExample("object/nightwatch/17")]
        [SiteIssue("The site publishes two examples as object/nightwatch/17; the JSON at that URL is the ownership page's 'permanent custody' example, not this non-specific relationship.")]
        public static HumanMadeObject Non_Specific_Relationship()
        {
            var nightwatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/17")
                .WithLabel("The Night Watch")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

            nightwatch.IdentifiedBy = [new Name("The Night Watch").AsPrimaryName()];

            nightwatch.AttributedBy = [
                new AttributeAssignment()
                {
                    IdentifiedBy = [
                        new Name("Related Object")
                            .WithClassifiedAs(Aat.DisplayName)
                    ],
                    Assigned = [
                        new HumanMadeObject()
                            .WithId("https://linked.art/example/object/rppob-28-106")
                            .WithLabel("Nachtwacht")
                    ],
                }
            ];

            return nightwatch;
        }


        [LinkedArtExample("person/bol/1")]
        public static Person Bol_Student_of_Rembrandt()
        {
            var bol = new Person()
                .WithContext()
                .WithId("https://linked.art/example/person/bol/1")
                .WithLabel("Ferdinand Bol");

            bol.IdentifiedBy = [new Name("Ferdinand Bol").AsPrimaryName()];

            bol.AttributedBy = [
                new AttributeAssignment()
                {
                    IdentifiedBy = [new Name("Student Of").WithClassifiedAs(Aat.DisplayName)],
                    Assigned = [ 
                        new Person()
                            .WithId("https://linked.art/example/person/rembrandt")
                            .WithLabel("Rembrandt")
                    ]
                }
            ];

            return bol;
        }
    }
}
