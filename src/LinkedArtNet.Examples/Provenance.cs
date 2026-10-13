
using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples
{
    public class Provenance
    {
        // https://linked.art/model/provenance/


        [LinkedArtExample("provenance/manet_proust/1")]
        public static Activity Spring_Sold_To_Proust()
        {
            var activity = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/provenance/manet_proust/1")
                .WithLabel("Purchase of Spring by Proust")
                .WithClassifiedAs(Aat.ProvenanceActivity);

            activity.IdentifiedBy = [new Name("Purchase of Spring by Proust from Manet").AsPrimaryName()];
            activity.TimeSpan = new LinkedArtTimeSpan()
            {
                BeginOfTheBegin = new LinkedArtDate(1881, 1, 1),
                EndOfTheEnd = new LinkedArtDate(1883, 12, 31).LastSecondOfDay()
            };

            var acquisition = new Acquisition()
            {
                Label = "Ownership of Spring to Proust",
                TransferredTitleOf = [
                    new HumanMadeObject()
                        .WithId("https://linked.art/example/object/spring")
                        .WithLabel("Spring")
                ],
                TransferredTitleFrom = [
                    new Person()
                        .WithId("https://linked.art/example/person/manet")
                        .WithLabel("Manet")
                ],
                TransferredTitleTo = [
                    new Person()
                        .WithId("https://linked.art/example/person/proust")
                        .WithLabel("Proust")
                ]
            };

            var payment = new Payment()
            {
                Label = "3000 Francs to Manet",
                PaidAmount = new MonetaryAmount
                {
                    Value = 3000,
                    Currency = Aat.Currency("300412016", "French Francs")
                },
                PaidFrom = [
                    new Person()
                        .WithId("https://linked.art/example/person/proust")
                        .WithLabel("Proust")
                ],
                PaidTo = [
                    new Person()
                        .WithId("https://linked.art/example/person/manet")
                        .WithLabel("Manet")
                ]
            };

            activity.Part = [
                acquisition,
                payment
            ];

            return activity;
        }


        [LinkedArtExample("provenance/ziwei_yuag/1")]
        public static Activity Gift_of_Landscape_to_YUAG()
        {
            var activity = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/provenance/ziwei_yuag/1")
                .WithLabel("Gift of Landscape to YUAG")
                .WithClassifiedAs(Aat.ProvenanceActivity)
                .WithClassifiedAs(Aat.Gift); 

            activity.TimeSpan = new LinkedArtTimeSpan()
            {
                BeginOfTheBegin = new LinkedArtDate(1999, 1, 1),
                EndOfTheEnd = new LinkedArtDate(1999, 12, 31).LastSecondOfDay()
            };

            var acquisition = new Acquisition()
            {
                Label = "Acquisition of Painting 1",
                TransferredTitleOf = [
                    new HumanMadeObject()
                        .WithId("https://linked.art/example/object/ziwei_landscape")
                        .WithLabel("Landscape")
                ],
                TransferredTitleFrom = [
                    new Person()
                        .WithId("https://linked.art/example/person/ziwei")
                        .WithLabel("Xu Ziwei")
                ],
                TransferredTitleTo = [
                    new Group()
                        .WithId("https://linked.art/example/group/yuag")
                        .WithLabel("Yale University Art Gallery")
                ]
            };

            activity.Part = [acquisition];

            return activity;
        }


        [LinkedArtExample("provenance/unknown_faure/1")]
        public static Activity Unknown_Acquisition_of_Spring_by_Faure()
        {
            var activity = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/provenance/unknown_faure/1")
                .WithLabel("Unknown Acquisition of Spring by Faure")
                .WithClassifiedAs(Aat.ProvenanceActivity);

            activity.After = [
                new Activity()
                    .WithId("https://linked.art/example/provenance/manet_proust")
                    .WithClassifiedAs(Aat.ProvenanceActivity)
            ];
            activity.Before = [
                new Activity()
                    .WithLabel("foure_durand")
                    .WithClassifiedAs(Aat.ProvenanceActivity)
            ];

            return activity;
        }
    }
}
