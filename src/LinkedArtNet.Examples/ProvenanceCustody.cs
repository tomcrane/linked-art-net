using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples
{
    /// <summary>https://linked.art/model/provenance/custody/</summary>
    public class ProvenanceCustody
    {
        [LinkedArtExample("provenance/spring_aic/1")]
        public static Activity Loan_Of_Spring_To_AIC()
        {
            var loan = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/provenance/spring_aic/1")
                .WithLabel("Loan of Spring to AIC")
                .WithClassifiedAs(Aat.ProvenanceActivity);

            loan.Part = [
                new TransferOfCustody
                {
                    TransferredCustodyOf = [
                        new HumanMadeObject()
                            .WithId("https://linked.art/example/object/spring")
                            .WithLabel("Spring")
                    ],
                    TransferredCustodyFrom = [
                        new Group()
                            .WithId("https://linked.art/example/group/getty")
                            .WithLabel("Getty Museum")
                    ],
                    TransferredCustodyTo = [
                        new Group()
                            .WithId("https://linked.art/example/group/aic")
                            .WithLabel("Art Institute of Chicago")
                    ]
                }.WithClassifiedAs(Aat.Term("300417645", "Loan"))
            ];

            return loan;
        }


        [LinkedArtExample("provenance/spring_getty/1")]
        public static Activity Return_Of_Spring_To_Getty()
        {
            var returnOfLoan = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/provenance/spring_getty/1")
                .WithLabel("Return of Spring to Getty")
                .WithClassifiedAs(Aat.ProvenanceActivity);

            returnOfLoan.Part = [
                new TransferOfCustody
                {
                    Label = "Return",
                    TransferredCustodyOf = [
                        new HumanMadeObject()
                            .WithId("https://linked.art/example/object/spring")
                            .WithLabel("Spring")
                    ],
                    TransferredCustodyFrom = [
                        new Group()
                            .WithId("https://linked.art/example/group/aic")
                            .WithLabel("Art Institute of Chicago")
                    ],
                    TransferredCustodyTo = [
                        new Group()
                            .WithId("https://linked.art/example/group/getty")
                            .WithLabel("Getty Museum")
                    ]
                }.WithClassifiedAs(Aat.Term("300438467", "Return of Loan"))
            ];

            return returnOfLoan;
        }


        [LinkedArtExample("provenance/mona_lisa_theft/1")]
        public static Activity Theft_Of_Mona_Lisa()
        {
            var theft = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/provenance/mona_lisa_theft/1")
                .WithLabel("Theft of Mona Lisa")
                .WithClassifiedAs(Aat.ProvenanceActivity);

            theft.Part = [
                new TransferOfCustody
                {
                    TransferredCustodyOf = [
                        new HumanMadeObject()
                            .WithId("https://linked.art/example/object/monalisa")
                            .WithLabel("Mona Lisa")
                    ],
                    TransferredCustodyFrom = [
                        new Group()
                            .WithId("https://linked.art/example/group/louvre")
                            .WithLabel("Louvre")
                    ],
                    TransferredCustodyTo = [
                        new Person()
                            .WithId("https://linked.art/example/person/peruggia")
                            .WithLabel("Vincenzo Peruggia")
                    ]
                }.WithClassifiedAs(Aat.Term("300055292", "Theft"))
            ];

            return theft;
        }


        [LinkedArtExample("provenance/sharp_sale/1")]
        public static Activity Purchase_Of_Stolen_Painting()
        {
            var purchase = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/provenance/sharp_sale/1")
                .WithLabel("Purchase of Stolen Painting")
                .WithClassifiedAs(Aat.ProvenanceActivity);

            var alter = new Person()
                .WithId("https://linked.art/example/person/alter")
                .WithLabel("Jerry Alter");

            purchase.Part = [
                new TransferOfCustody
                {
                    Label = "Sale of Painting",
                    TransferredCustodyOf = [
                        new HumanMadeObject()
                            .WithId("https://linked.art/example/object/cheyenne")
                            .WithLabel("Cheyenne Oklahoma")
                    ],
                    TransferredCustodyFrom = [alter]
                }.WithClassifiedAs(Aat.Term("300438484", "sale of stolen goods")),
                new Payment
                {
                    PaidAmount = new MonetaryAmount
                    {
                        Label = "Payment to Thief",
                        Value = 52650,
                        Currency = Aat.UsDollars.WithNotation("USD")
                    },
                    PaidTo = [alter]
                }
            ];

            return purchase;
        }
    }
}
