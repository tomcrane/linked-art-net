using LinkedArtNet;
using LinkedArtNet.Vocabulary;

namespace Examples.NewDocExamples
{
    public class ProductionDestruction
    {
        public static void Create()
        {
            // https://linked.art/model/object/production/

            NightWatch_5_Production();
            Bust_1_Technique();
            RUN_1_Multiple_Artists();
            KellerDana_1_Influenced();
            OKeeffe_Gok_1();
            OKeeffe_Yuag_1();
            Canterbury_1();
            Canterbury_Plate_1();
            Coppa_1_Unknown();
            Washday_1_Influenced();
            HarwoodStudio_1_Group();
            GradualPage_1_PartRemoval();
            LePeintre_1_Destruction();
            LePeintre_2_Destruction_CausedBy();
        }


        private static void NightWatch_5_Production()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/nightwatch/5")
                .WithLabel("Night Watch by Rembrandt");

            nightWatch.ProducedBy = new Activity(Types.Production)
                {
                    TimeSpan = LinkedArtTimeSpan.FromYear(1642),
                    TookPlaceAt = [
                        new Place()
                        .WithId($"{Documentation.IdRoot}/place/amsterdam")
                        .WithLabel("Amsterdam")
                    ],
                    CarriedOutBy = [
                        new Person()
                        .WithId($"{Documentation.IdRoot}/person/rembrandt")
                        .WithLabel("Rembrandt")
                    ]
                };

            Documentation.Save(nightWatch);
        }

        private static void Bust_1_Technique()
        {
            var bust = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/bust/1")
                .WithLabel("Bust of a Man")
                .WithClassifiedAs(Getty.Sculpture, Getty.TypeOfWork);

            bust.ProducedBy = new Activity(Types.Production)
                {
                    Technique = [Getty.AatType("Sculpting", "300264383")],
                    CarriedOutBy = [
                        new LinkedArtObject(Types.Group)
                            .WithId($"{Documentation.IdRoot}/group/harwoodstudio")
                            .WithLabel("Studio of Francis Harwood")]
                };
        
            Documentation.Save(bust);
        }


        private static void RUN_1_Multiple_Artists()
        {
            var run = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/run/1")
                .WithLabel("RUN")
                .WithClassifiedAs(Getty.Painting, Getty.TypeOfWork);

            run.ProducedBy = new Activity(Types.Production)
            {
                Part = [
                    new Activity(Types.Production)
                    {
                        Technique = [Getty.AatType("Painting", "300054216")],
                        CarriedOutBy = [
                            new Person()
                                .WithId($"{Documentation.IdRoot}/person/barrow")
                                .WithLabel("Mark Barrow")
                        ]
                    },
                    new Activity(Types.Production)
                    {
                        Technique = [Getty.AatType("hand weaving", "300053643")],
                        CarriedOutBy = [
                            new Person()
                                .WithId($"{Documentation.IdRoot}/person/parke")
                                .WithLabel("Sarah Parke")
                        ]
                    }
                ]
            };

            Documentation.Save(run);
        }


        private static void KellerDana_1_Influenced()
        {
            var dana = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/kellerdana/1")
                .WithLabel("Copy of Huntington Portrait")
                .WithClassifiedAs(Getty.Painting, Getty.TypeOfWork);

            dana.ProducedBy =
                new Activity(Types.Production)
                {
                    CarriedOutBy = [
                        new Person()
                            .WithId($"{Documentation.IdRoot}/person/keller")
                            .WithLabel("Deane Keller")
                    ],
                    InfluencedBy = [
                        new HumanMadeObject()
                            .WithId($"{Documentation.IdRoot}/object/huntingtondana")
                            .WithLabel("Huntington Portrait of Dana")
                    ]
                };

            Documentation.Save(dana);
        }


        private static void OKeeffe_Gok_1()
        {
            var gok = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/okeeffe-gok/1")
                .WithLabel("GOK 1918, GOKM")
                .WithClassifiedAs(Getty.Photograph, Getty.TypeOfWork);

            gok.IdentifiedBy = [
                new LinkedArtObject(Types.Identifier)
                    .WithContent("2014.3.78")
                    .WithClassifiedAs(Getty.AatType("Accession Number", "300312355"))
            ];

            gok.Shows = [
                new Work(Types.VisualItem)
                    .WithId($"{Documentation.IdRoot}/visual/okeeffe")
                    .WithLabel("Visual Content of GOK 1918")
            ];

            gok.ProducedBy = new Activity(Types.Production).WithLabel("Printing of Photograph");
            gok.ProducedBy.UsedSpecificObject = [
                new HumanMadeObject()
                    .WithId($"{Documentation.IdRoot}/object/okeeffe-negative")
                    .WithLabel("Negative of GOK 1918")
            ];

            Documentation.Save(gok);
        }

        private static void OKeeffe_Yuag_1()
        {
            var gok = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/okeeffe-yuag/1")
                .WithLabel("GOK 1918, YUAG")
                .WithClassifiedAs(Getty.Photograph, Getty.TypeOfWork);

            gok.IdentifiedBy = [
                new LinkedArtObject(Types.Identifier)
                    .WithContent("2016.101.242")
                    .WithClassifiedAs(Getty.AatType("Accession Number", "300312355"))
            ];

            gok.Shows = [
                new Work(Types.VisualItem)
                    .WithId($"{Documentation.IdRoot}/visual/okeeffe")
                    .WithLabel("Visual Content of GOK 1918")
            ];

            gok.ProducedBy = new Activity(Types.Production).WithLabel("Printing of Photograph");
            gok.ProducedBy.UsedSpecificObject = [
                new HumanMadeObject()
                    .WithId($"{Documentation.IdRoot}/object/okeeffe-negative")
                    .WithLabel("Negative of GOK 1918")
            ];

            Documentation.Save(gok);
        }


        private static void Canterbury_1()
        {
            var ccp = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/ccp/1")
                .WithLabel("Chaucer's Canterbury Pilgrims")
                .WithClassifiedAs(Getty.Print, Getty.TypeOfWork);

            ccp.ProducedBy = new Activity(Types.Production).WithLabel("Printing from Plate");
            ccp.ProducedBy.UsedSpecificObject = [
                new HumanMadeObject()
                    .WithId($"{Documentation.IdRoot}/object/ccp-plate")
                    .WithLabel("Plate for CCP")
            ];

            Documentation.Save(ccp);
        }


        private static void Canterbury_Plate_1()
        {
            var plate = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/ccp-plate/1")
                .WithLabel("Plate for CCP")
                .WithMadeOf("copper", "300011020");

            Documentation.Save(plate);
        }


        private static void Coppa_1_Unknown()
        {
            var coppa = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/coppa/1")
                .WithLabel("Coppa Amatoria");

            coppa.IdentifiedBy = [
                new LinkedArtObject(Types.Name)
                    .WithContent("Coppa Amatoria")
                    .AsPrimaryName()
            ];

            coppa.ProducedBy = new Activity(Types.Production)
            {
                CarriedOutBy = [
                    new LinkedArtObject(Types.Group)
                        .WithId($"{Documentation.IdRoot}/group/unknown_italian")
                        .WithLabel("Unidentified Italian")
                ]
            };

            Documentation.Save(coppa);
        }


        private static void Washday_1_Influenced()
        {
            var washday = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/washday/1")
                .WithLabel("Wash Day")
                .WithClassifiedAs(Getty.Painting, Getty.TypeOfWork);

            washday.IdentifiedBy = [
                new LinkedArtObject(Types.Name)
                    .WithContent("Wash Day")
                    .AsPrimaryName()
            ];

            washday.ProducedBy = new Activity(Types.Production)
            {
                InfluencedBy = [
                    new Person()
                        .WithId($"{Documentation.IdRoot}/person/whomer")
                        .WithLabel("Winslow Homer")
                ]
            };

            Documentation.Save(washday);
        }

        private static void HarwoodStudio_1_Group()
        {
            var grp = new LinkedArtObject(Types.Group)
                .WithContext()
                .WithId($"{Documentation.IdRoot}/group/harwoodstudio/1")
                .WithLabel("Studio of Francis Harwood")
                .WithClassifiedAs(Getty.AatType("Studio", "300404275"));

            grp.FormedBy = new Activity(Types.Formation)
            {
                InfluencedBy = [
                    new Person()
                        .WithId("http://vocab.getty.edu/ulan/500015886")
                        .WithLabel("Francis Harwood")
                ]
            };

            Documentation.Save(grp);
        }


        private static void GradualPage_1_PartRemoval()
        {
            var gradualPage = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/gradualpage/1")
                .WithLabel("Page from a Gradual")
                .WithClassifiedAs(Getty.AatType("Page", "300194222"), Getty.TypeOfWork);

            gradualPage.RemovedBy = [
                new Activity(Types.PartRemoval)
                {
                    Diminished = new HumanMadeObject()
                        .WithId($"{Documentation.IdRoot}/object/gradual")
                        .WithLabel("Gradual")
                }
            ];

            Documentation.Save(gradualPage);
        }


        private static void LePeintre_1_Destruction()
        {
            var lepeintre = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/lepeintre/1")
                .WithLabel("Le Peintre by Picasso")
                .WithClassifiedAs(Getty.Painting, Getty.TypeOfWork);

            var destruction = new Activity(Types.Destruction)
                .WithLabel("Destruction of Le Peintre");
            destruction.TimeSpan = new LinkedArtTimeSpan
            {
                BeginOfTheBegin = new LinkedArtDate(new DateTimeOffset(1998, 9, 2, 22, 20, 0, TimeSpan.Zero)),
                EndOfTheEnd = new LinkedArtDate(new DateTimeOffset(1998, 9, 2, 22, 40, 0, TimeSpan.Zero))
            };
            lepeintre.DestroyedBy = destruction;

            // https://linked.art/example/object/lepeintre/1.json
            // This has end_of_the_end "1998-09-02T022:40:00Z"
            //                                     ^  (typo?)
            Documentation.Save(lepeintre, false);
        }

        private static void LePeintre_2_Destruction_CausedBy()
        {
            var lepeintre2 = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/lepeintre/2")
                .WithLabel("Le Peintre by Picasso")
                .WithClassifiedAs(Getty.Painting, Getty.TypeOfWork);

            var destruction = new Activity(Types.Destruction)
                .WithLabel("Destruction of Le Peintre");
            destruction.TimeSpan = new LinkedArtTimeSpan
            {
                BeginOfTheBegin = new LinkedArtDate(new DateTimeOffset(1998, 9, 2, 22, 20, 0, TimeSpan.Zero)),
                EndOfTheEnd = new LinkedArtDate(new DateTimeOffset(1998, 9, 2, 22, 40, 0, TimeSpan.Zero))
            };
            destruction.CausedBy = [
                new LinkedArtObject(Types.Event)
                    .WithId($"{Documentation.IdRoot}/event/sr111crash")
                    .WithLabel("Crash of Swiss Air 111")
            ];
            lepeintre2.DestroyedBy = destruction;
            Documentation.Save(lepeintre2);
        }
    }
}