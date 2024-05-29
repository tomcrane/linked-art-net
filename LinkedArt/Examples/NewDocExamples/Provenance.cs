
using LinkedArtNet;
using LinkedArtNet.Vocabulary;

namespace Examples.NewDocExamples
{
    public class Provenance
    {
        // https://linked.art/model/provenance/

        public static void Create()
        {
            Spring_Sold_To_Proust();
            Gift_of_Landscape_to_YUAG();
            Unknown_Acquisition_of_Spring_by_Faure();
        }


        private static void Spring_Sold_To_Proust()
        {
            var activity = new Activity()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/provenance/manet_proust/1")
                .WithLabel("Purchase of Spring by Proust")
                .WithClassifiedAs(Getty.ProvenanceActivity);

            activity.IdentifiedBy = [new Name("Purchase of Spring by Proust from Manet").AsPrimaryName()];
            activity.TimeSpan = new LinkedArtTimeSpan()
            {
                BeginOfTheBegin = new LinkedArtDate(1881, 1, 1),
                EndOfTheEnd = new LinkedArtDate(1883, 12, 31).LastSecondOfDay()
            };

            var acquisition = new Activity(Types.Acquisition)
            {
                Label = "Ownership of Spring to Proust",
                TransferredTitleOf = [
                    new HumanMadeObject()
                        .WithId($"{Documentation.IdRoot}/object/spring")
                        .WithLabel("Spring")
                ],
                TransferredTitleFrom = [
                    new Person()
                        .WithId($"{Documentation.IdRoot}/person/manet")
                        .WithLabel("Manet")
                ],
                TransferredTitleTo = [
                    new Person()
                        .WithId($"{Documentation.IdRoot}/person/proust")
                        .WithLabel("Proust")
                ]
            };

            var payment = new Payment()
            {
                Label = "3000 Francs to Manet",
                PaidAmount = new MonetaryAmount
                {
                    Value = 3000,
                    Currency = new LinkedArtObject(Types.Currency)
                        .WithId($"{Getty.Aat}300412016")
                        .WithLabel("French Francs")
                },
                PaidFrom = [
                    new Person()
                        .WithId($"{Documentation.IdRoot}/person/proust")
                        .WithLabel("Proust")
                ],
                PaidTo = [
                    new Person()
                        .WithId($"{Documentation.IdRoot}/person/manet")
                        .WithLabel("Manet")
                ]
            };

            activity.Part = [
                acquisition,
                payment
            ];

            Documentation.Save(activity);
        }


        private static void Gift_of_Landscape_to_YUAG()
        {
            var activity = new Activity()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/provenance/ziwei_yuag/1")
                .WithLabel("Gift of Landscape to YUAG")
                .WithClassifiedAs(Getty.ProvenanceActivity)
                .WithClassifiedAs($"{Getty.Aat}300417637", null); // no label in example

            activity.TimeSpan = new LinkedArtTimeSpan()
            {
                BeginOfTheBegin = new LinkedArtDate(1999, 1, 1),
                EndOfTheEnd = new LinkedArtDate(1999, 12, 31).LastSecondOfDay()
            };

            var acquisition = new Activity(Types.Acquisition)
            {
                Label = "Acquisition of Painting 1",
                TransferredTitleOf = [
                    new HumanMadeObject()
                        .WithId($"{Documentation.IdRoot}/object/ziwei_landscape")
                        .WithLabel("Lanscape")
                ],
                TransferredTitleFrom = [
                    new Person()
                        .WithId($"{Documentation.IdRoot}/person/ziwei")
                        .WithLabel("Xu Ziwei")
                ],
                TransferredTitleTo = [
                    new Group()
                        .WithId($"{Documentation.IdRoot}/group/yuag")
                        .WithLabel("Yale University Art Gallery")
                ]
            };

            activity.Part = [acquisition];

            Documentation.Save(activity);
        }


        private static void Unknown_Acquisition_of_Spring_by_Faure()
        {
            var activity = new Activity()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/provenance/unknown_faure/1")
                .WithLabel("Unknown Acquisition of Spring by Faure")
                .WithClassifiedAs(Getty.ProvenanceActivity);

            activity.StartsAfterTheEndOf = [
                new Activity()
                    .WithId($"{Documentation.IdRoot}/provenance/manet_proust")
                    .WithClassifiedAs(Getty.ProvenanceActivity)
            ];
            activity.EndsBeforeTheStartOf = [
                new Activity()
                    .WithLabel("foure_durand")
                    .WithClassifiedAs(Getty.ProvenanceActivity)
            ];

            Documentation.Save(activity);
        }
    }
}
