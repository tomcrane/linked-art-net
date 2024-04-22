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


        }


        private static void NightWatch_5_Production()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/nightwatch/5")
                .WithLabel("Night Watch by Rembrandt");

            nightWatch.ProducedBy = new Activity(Types.Production)
                {
                    TimeSpan = [LinkedArtTimeSpan.FromYear(1642)],
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
                .WithClassifiedAs(Getty.Sculpture);
            bust.ClassifiedAs![0].WithClassifiedAs(Getty.TypeOfWork);

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
                .WithClassifiedAs(Getty.Painting);
            run.ClassifiedAs![0].WithClassifiedAs(Getty.TypeOfWork);

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
                .WithClassifiedAs(Getty.Painting); 
            dana.ClassifiedAs![0].WithClassifiedAs(Getty.TypeOfWork);

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
                .WithClassifiedAs(Getty.Photograph);
            gok.ClassifiedAs![0].WithClassifiedAs(Getty.TypeOfWork);

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
                .WithClassifiedAs(Getty.Photograph);
            gok.ClassifiedAs![0].WithClassifiedAs(Getty.TypeOfWork);

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
    }
}