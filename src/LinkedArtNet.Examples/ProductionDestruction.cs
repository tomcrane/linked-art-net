using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples
{
    public class ProductionDestruction
    {
        // https://linked.art/model/object/production/


        [LinkedArtExample("object/nightwatch/5")]
        public static HumanMadeObject NightWatch_5_Production()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/5")
                .WithLabel("Night Watch by Rembrandt");

            nightWatch.ProducedBy = new Production()
                {
                    TimeSpan = LinkedArtTimeSpan.FromYear(1642).WithLabel("1642"),
                    TookPlaceAt = [
                        new Place()
                        .WithId("https://linked.art/example/place/amsterdam")
                        .WithLabel("Amsterdam")
                    ],
                    CarriedOutBy = [
                        new Person()
                        .WithId("https://linked.art/example/person/rembrandt")
                        .WithLabel("Rembrandt")
                    ]
                };

            return nightWatch;
        }

        [LinkedArtExample("object/bust/1")]
        public static HumanMadeObject Bust_1_Technique()
        {
            var bust = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/bust/1")
                .WithLabel("Bust of a Man")
                .WithClassifiedAs(Aat.Sculpture, Aat.TypeOfWork);

            bust.ProducedBy = new Production()
                {
                    Technique = [Aat.Term("300264383", "Sculpting")],
                    CarriedOutBy = [
                        Ulan.Person("500015886", "Francis Harwood")]
                };
        
            return bust;
        }


        [LinkedArtExample("object/run/1")]
        public static HumanMadeObject RUN_1_Multiple_Artists()
        {
            var run = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/run/1")
                .WithLabel("RUN")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

            run.ProducedBy = new Production()
            {
                Part = [
                    new Production()
                    {
                        Technique = [Aat.Term("300054216", "Painting")],
                        CarriedOutBy = [
                            new Person()
                                .WithId("https://linked.art/example/person/barrow")
                                .WithLabel("Mark Barrow")
                        ]
                    },
                    new Production()
                    {
                        Technique = [Aat.Term("300053643", "hand weaving")],
                        CarriedOutBy = [
                            new Person()
                                .WithId("https://linked.art/example/person/parke")
                                .WithLabel("Sarah Parke")
                        ]
                    }
                ]
            };

            return run;
        }


        [LinkedArtExample("object/kellerdana/1")]
        public static HumanMadeObject KellerDana_1_Influenced()
        {
            var dana = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/kellerdana/1")
                .WithLabel("Copy of Huntington Portrait")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

            dana.ProducedBy =
                new Production()
                {
                    CarriedOutBy = [
                        new Person()
                            .WithId("https://linked.art/example/person/keller")
                            .WithLabel("Deane Keller")
                    ],
                    InfluencedBy = [
                        new HumanMadeObject()
                            .WithId("https://linked.art/example/object/huntingtondana")
                            .WithLabel("Huntington Portrait of Dana")
                    ]
                };

            return dana;
        }


        [LinkedArtExample("object/okeeffe-gok/1")]
        public static HumanMadeObject OKeeffe_Gok_1()
        {
            var gok = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/okeeffe-gok/1")
                .WithLabel("GOK 1918, GOKM")
                .WithClassifiedAs(Aat.Photograph, Aat.TypeOfWork);

            gok.IdentifiedBy = [ new Identifier("2014.3.78").AsAccessionNumber() ];

            gok.Shows = [
                new VisualItem()
                    .WithId("https://linked.art/example/visual/okeeffe")
                    .WithLabel("Visual Content of GOK 1918")
            ];

            gok.ProducedBy = new Production().WithLabel("Printing of Photograph");
            gok.ProducedBy.UsedSpecificObject = [
                new HumanMadeObject()
                    .WithId("https://linked.art/example/object/okeeffe-negative")
                    .WithLabel("Negative of GOK 1918")
            ];

            return gok;
        }

        [LinkedArtExample("object/okeeffe-yuag/1")]
        public static HumanMadeObject OKeeffe_Yuag_1()
        {
            var gok = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/okeeffe-yuag/1")
                .WithLabel("GOK 1918, YUAG")
                .WithClassifiedAs(Aat.Photograph, Aat.TypeOfWork);

            gok.IdentifiedBy = [ new Identifier("2016.101.242").AsAccessionNumber() ];

            gok.Shows = [
                new VisualItem()
                    .WithId("https://linked.art/example/visual/okeeffe")
                    .WithLabel("Visual Content of GOK 1918")
            ];

            gok.ProducedBy = new Production().WithLabel("Printing of Photograph");
            gok.ProducedBy.UsedSpecificObject = [
                new HumanMadeObject()
                    .WithId("https://linked.art/example/object/okeeffe-negative")
                    .WithLabel("Negative of GOK 1918")
            ];

            return gok;
        }


        [LinkedArtExample("object/ccp/1")]
        public static HumanMadeObject Canterbury_1()
        {
            var ccp = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/ccp/1")
                .WithLabel("Chaucer's Canterbury Pilgrims")
                .WithClassifiedAs(Aat.Print, Aat.TypeOfWork);

            ccp.ProducedBy = new Production().WithLabel("Printing from Plate");
            ccp.ProducedBy.UsedSpecificObject = [
                new HumanMadeObject()
                    .WithId("https://linked.art/example/object/ccp-plate")
                    .WithLabel("Plate for CCP")
            ];

            return ccp;
        }


        [LinkedArtExample("object/ccp-plate/1")]
        public static HumanMadeObject Canterbury_Plate_1()
        {
            var plate = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/ccp-plate/1")
                .WithLabel("Plate for CCP")
                .WithMadeOf("copper", "300011020");

            return plate;
        }


        [LinkedArtExample("object/nuveen/1")]
        public static HumanMadeObject Nuveen_1()
        {
            var nuveenPainting = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nuveen/1")
                .WithLabel("Nuveen Painting");

            nuveenPainting.IdentifiedBy = [
                new Name("The Nuveen Painting").AsPrimaryName()
            ];

            nuveenPainting.ProducedBy = new Production()
            {
                CarriedOutBy = [
                    new Person()
                        .WithId("https://linked.art/example/person/dine")
                        .WithLabel("Jim Dine")
                ],
                CausedBy = [
                    new Activity()
                        .WithId("https://linked.art/example/event/nuveen_commission")
                        .WithLabel("Commission")
                ]
            };

            return nuveenPainting;

        }


        [LinkedArtExample("object/coppa/1")]
        public static HumanMadeObject Coppa_1_Unknown()
        {
            var coppa = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/coppa/1")
                .WithLabel("Coppa Amatoria");

            coppa.IdentifiedBy = [
                new Name("Coppa Amatoria").AsPrimaryName()
            ];

            coppa.ProducedBy = new Production()
            {
                CarriedOutBy = [
                    new Group()
                        .WithId("https://linked.art/example/group/unknown_italian")
                        .WithLabel("Unidentified Italian")
                ]
            };

            return coppa;
        }


        [LinkedArtExample("object/washday/1")]
        public static HumanMadeObject Washday_1_Influenced()
        {
            var washday = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/washday/1")
                .WithLabel("Wash Day")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

            washday.IdentifiedBy = [
                new Name("Wash Day").AsPrimaryName()
            ];

            washday.ProducedBy = new Production()
            {
                InfluencedBy = [
                    new Person()
                        .WithId("https://linked.art/example/person/whomer")
                        .WithLabel("Winslow Homer")
                ]
            };

            return washday;
        }

        [LinkedArtExample("group/harwoodstudio/1")]
        public static Group HarwoodStudio_1_Group()
        {
            var grp = new Group()
                .WithContext()
                .WithId("https://linked.art/example/group/harwoodstudio/1")
                .WithLabel("Studio of Francis Harwood")
                .WithClassifiedAs(Aat.StudioOf);

            grp.FormedBy = new Formation()
            {
                InfluencedBy = [
                    Ulan.Person("500015886", "Francis Harwood")
                ]
            };

            return grp;
        }


        [LinkedArtExample("object/gradualpage/1")]
        public static HumanMadeObject GradualPage_1_PartRemoval()
        {
            var gradualPage = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/gradualpage/1")
                .WithLabel("Page from a Gradual")
                .WithClassifiedAs(Aat.TextOfAPage, Aat.TypeOfWork);

            gradualPage.RemovedBy = [
                new PartRemoval()
                {
                    Diminished = new HumanMadeObject()
                        .WithId("https://linked.art/example/object/gradual")
                        .WithLabel("Gradual")
                }
            ];

            return gradualPage;
        }


        [LinkedArtExample("object/torosaurus/1")]
        public static HumanMadeObject Torosaurus_Gladius()
        {
            var torosaurus = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/torosaurus/1")
                .WithLabel("Torosaurus Gladius");

            torosaurus.EncounteredBy = [
                new Encounter()
                {
                    TimeSpan = LinkedArtTimeSpan.FromYear(1891),
                    CarriedOutBy = [
                        new Person()
                            .WithId("https://linked.art/example/person/hatcher")
                            .WithLabel("John Bell Hatcher")
                    ]
                }
            ];

            // The docs example doesn't have a label "1891" on the timespan:

            return torosaurus;
        }


        [LinkedArtExample("object/lepeintre/1")]
        public static HumanMadeObject LePeintre_1_Destruction()
        {
            var lepeintre = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/lepeintre/1")
                .WithLabel("Le Peintre by Picasso")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

            var destruction = new Destruction()
                .WithLabel("Destruction of Le Peintre");
            destruction.TimeSpan = new LinkedArtTimeSpan
            {
                BeginOfTheBegin = new LinkedArtDate(new DateTimeOffset(1998, 9, 2, 22, 20, 0, TimeSpan.Zero)),
                EndOfTheEnd = new LinkedArtDate(new DateTimeOffset(1998, 9, 2, 22, 40, 0, TimeSpan.Zero))
            };
            lepeintre.DestroyedBy = destruction;

            return lepeintre;
        }

        [LinkedArtExample("object/lepeintre/2")]
        public static HumanMadeObject LePeintre_2_Destruction_CausedBy()
        {
            var lepeintre2 = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/lepeintre/2")
                .WithLabel("Le Peintre by Picasso")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

            var destruction = new Destruction()
                .WithLabel("Destruction of Le Peintre");
            destruction.TimeSpan = new LinkedArtTimeSpan
            {
                BeginOfTheBegin = new LinkedArtDate(new DateTimeOffset(1998, 9, 2, 22, 20, 0, TimeSpan.Zero)),
                EndOfTheEnd = new LinkedArtDate(new DateTimeOffset(1998, 9, 2, 22, 40, 0, TimeSpan.Zero))
            };
            destruction.CausedBy = [
                new Event()
                    .WithId("https://linked.art/example/event/sr111crash")
                    .WithLabel("Crash of Swiss Air 111")
            ];
            lepeintre2.DestroyedBy = destruction;
            return lepeintre2;
        }
    }
}