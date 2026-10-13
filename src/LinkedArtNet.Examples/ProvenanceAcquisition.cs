using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples
{
    /// <summary>https://linked.art/model/provenance/acquisition/</summary>
    public class ProvenanceAcquisition
    {
        [LinkedArtExample("provenance/durand_payne/1")]
        public static Activity Purchase_Of_Spring_By_Payne()
        {
            var purchase = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/provenance/durand_payne/1")
                .WithLabel("Purchase of Spring by Payne")
                .WithClassifiedAs(Aat.ProvenanceActivity);

            purchase.IdentifiedBy = [new Name("Purchase of Spring by Payne from Durand-Ruel").AsPrimaryName()];
            purchase.TimeSpan = LinkedArtTimeSpan.FromYear(1909);

            purchase.Part = [
                new Acquisition
                {
                    Label = "Ownership of Spring to Payne",
                    TransferredTitleOf = [
                        new HumanMadeObject()
                            .WithId("https://linked.art/example/object/spring")
                            .WithLabel("Spring")
                    ],
                    TransferredTitleFrom = [
                        new Person()
                            .WithId("https://linked.art/example/person/durand")
                            .WithLabel("Durand-Ruel Gallery")
                    ],
                    TransferredTitleTo = [
                        new Person()
                            .WithId("https://linked.art/example/person/payne")
                            .WithLabel("Oliver Payne")
                    ]
                }
            ];

            return purchase;
        }


        [LinkedArtExample("provenance/payneheirs_getty/1")]
        public static Activity Purchase_Through_An_Agent()
        {
            var purchase = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/provenance/payneheirs_getty/1")
                .WithLabel("Purchase of Spring for Getty")
                .WithClassifiedAs(Aat.ProvenanceActivity);

            purchase.Part = [
                new Acquisition
                {
                    Label = "Acquisition of Spring",
                    CarriedOutBy = [
                        new Person()
                            .WithId("https://linked.art/example/person/naumann")
                            .WithLabel("Otto Naumann")
                    ],
                    TransferredTitleOf = [
                        new HumanMadeObject()
                            .WithId("https://linked.art/example/object/spring")
                            .WithLabel("Spring")
                    ],
                    TransferredTitleFrom = [
                        new Group()
                            .WithId("https://linked.art/example/group/payne_heirs")
                            .WithLabel("Family of Oliver Payne")
                    ],
                    TransferredTitleTo = [
                        new Group()
                            .WithId("https://linked.art/example/group/getty")
                            .WithLabel("J. Paul Getty Museum")
                    ]
                }
            ];

            return purchase;
        }


        [LinkedArtExample("provenance/yuag_ycba_wiley/1")]
        public static Activity Joint_Purchase()
        {
            var purchase = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/provenance/yuag_ycba_wiley/1")
                .WithLabel("Purchase of Painting")
                .WithClassifiedAs(Aat.ProvenanceActivity);

            purchase.TimeSpan = LinkedArtTimeSpan.FromYear(2021);

            purchase.Part = [
                new Acquisition
                {
                    TransferredTitleOf = [
                        new HumanMadeObject()
                            .WithId("https://linked.art/example/object/yiadom-boakye")
                            .WithLabel("Portrait of Lynette Yiadom-Boakye")
                    ],
                    TransferredTitleTo = [
                        new Group()
                            .WithId("https://linked.art/example/group/yuag")
                            .WithLabel("Yale University Art Gallery"),
                        new Group()
                            .WithId("https://linked.art/example/group/ycba")
                            .WithLabel("Yale Center for British Art")
                    ]
                }
            ];

            return purchase;
        }


        [LinkedArtExample("provenance/yuag_stora/1")]
        public static Activity Exchange_Of_Objects()
        {
            var exchange = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/provenance/yuag_stora/1")
                .WithLabel("Exchange of Objects")
                .WithClassifiedAs(Aat.ProvenanceActivity);

            var feigen = new Group()
                .WithId("https://linked.art/example/group/feigen")
                .WithLabel("Richard Feigen Gallery");
            var yuag = new Group()
                .WithId("https://linked.art/example/group/yuag")
                .WithLabel("Yale University Art Gallery");

            exchange.Part = [
                new Acquisition
                {
                    Label = "Acquisition of Personnages",
                    TransferredTitleOf = [
                        new HumanMadeObject()
                            .WithId("https://linked.art/example/object/masson_personnages")
                            .WithLabel("Personnages dans un parc"),
                        new HumanMadeObject()
                            .WithId("https://linked.art/example/object/beckmann_nude")
                            .WithLabel("Nude Woman by Beckmann")
                    ],
                    TransferredTitleFrom = [feigen, yuag],
                    TransferredTitleTo = [yuag, feigen]
                },
                new Acquisition
                {
                    Label = "Acquisition of Nude"
                }
            ];

            return exchange;
        }


        [LinkedArtExample("provenance/fixme/1")]
        public static Activity Purchase_With_Commission()
        {
            var purchase = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/provenance/fixme/1")
                .WithLabel("Purchase of Sculpture with Commission")
                .WithClassifiedAs(Aat.ProvenanceActivity);

            var buyer = new Person().WithLabel("Buyer");

            purchase.Part = [
                new Payment
                {
                    PaidAmount = new MonetaryAmount
                    {
                        Value = 900,
                        Currency = Aat.UsDollars.WithNotation("USD")
                    },
                    PaidFrom = [buyer],
                    PaidTo = [new Person().WithLabel("Seller")]
                },
                new Payment
                {
                    PaidAmount = new MonetaryAmount
                    {
                        Value = 100,
                        Currency = Aat.UsDollars
                    },
                    PaidFrom = [buyer],
                    PaidTo = [
                        new Group()
                            .WithLabel("Auction House")
                            .WithClassifiedAs(Aat.Term("300417515", "Auction House (organization)"))
                    ]
                }.WithClassifiedAs(Aat.Term("300393199", "Commission"))
            ];

            return purchase;
        }
    }
}
