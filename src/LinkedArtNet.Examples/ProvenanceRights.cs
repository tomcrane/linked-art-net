using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples
{
    /// <summary>https://linked.art/model/provenance/rights/</summary>
    public class ProvenanceRights
    {
        private static LinkedArtNet.Type OwnershipRight => Aat.Term("300055603", "Ownership Right");

        private static MeasurementUnit Percent => new MeasurementUnit("http://qudt.org/3.0.0/vocab/unit/Percent", "Percent");

        private static HumanMadeObject Painting => new HumanMadeObject()
            .WithLabel("Painting")
            .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

        private static Right Share(string label, double percent, string owner) => new Right()
            .WithLabel(label)
            .WithClassifiedAs(OwnershipRight)
            .WithDimension(new Dimension { Value = percent, Unit = Percent })
            .WithPossessedBy(new Group().WithLabel(owner));


        [LinkedArtExample("provenance/2")]
        public static Activity Acquisition_Establishes_Ownership_Right()
        {
            var activity = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/provenance/2")
                .WithClassifiedAs(Aat.ProvenanceActivity);

            var painting = new HumanMadeObject().WithLabel("Painting");
            var museum = new Group().WithLabel("Museum");

            activity.Part = [
                new Acquisition
                {
                    TransferredTitleOf = [painting],
                    TransferredTitleTo = [museum]
                },
                new RightAcquisition
                {
                    Establishes = [
                        new Right()
                            .WithLabel("Right of Ownership for Painting")
                            .WithClassifiedAs(OwnershipRight)
                            .WithPossessedBy(museum)
                            .WithAppliesTo(painting)
                    ]
                }
            ];

            return activity;
        }


        [LinkedArtExample("provenance/3")]
        public static Activity Shared_Ownership()
        {
            var purchase = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/provenance/3")
                .WithLabel("Purchase of Painting by Two Dealers")
                .WithClassifiedAs(Aat.ProvenanceActivity);

            purchase.Part = [
                new RightAcquisition
                {
                    Establishes = [
                        new Right()
                            .WithLabel("Total Right of Ownership")
                            .WithClassifiedAs(OwnershipRight)
                            .WithAppliesTo(Painting)
                            .WithPart(
                                Share("30% Ownership by Dealer 1", 30, "Dealer 1"),
                                Share("70% Ownership by Dealer 2", 70, "Dealer 2"))
                    ]
                }
            ];

            return purchase;
        }


        [LinkedArtExample("provenance/4")]
        public static Activity Trading_Shares()
        {
            var trade = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/provenance/4")
                .WithLabel("Trading Shares of Painting between Two Dealers")
                .WithClassifiedAs(Aat.ProvenanceActivity);

            trade.Part = [
                new RightAcquisition
                {
                    Establishes = [Share("40% Ownership by Dealer 1", 40, "Dealer 1").WithAppliesTo(Painting)],
                    Invalidates = [new Right().WithLabel("30% Ownership by Dealer 1").WithClassifiedAs(OwnershipRight)]
                },
                new RightAcquisition
                {
                    // The site's example gives Dealer 2's new 60% share a value of 70.
                    Establishes = [Share("60% Ownership by Dealer 2", 70, "Dealer 2")],
                    Invalidates = [new Right().WithLabel("70% Ownership by Dealer 2").WithClassifiedAs(OwnershipRight)]
                }
            ];

            return trade;
        }


        [LinkedArtExample("provenance/5")]
        public static Activity Copyright_Acquisition()
        {
            var activity = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/provenance/5")
                .WithLabel("Copyright acquisition by PCA")
                .WithClassifiedAs(Aat.ProvenanceActivity);

            activity.Part = [
                new RightAcquisition
                {
                    Establishes = [
                        new Right()
                            .WithClassifiedAs(Aat.Term("300055598", "Copyright"))
                            .WithPossessedBy(new Group().WithLabel("Paintings Copyright Association"))
                            .WithAppliesTo(Painting.WithClassifiedAs(Aat.Artwork))
                    ]
                }
            ];

            return activity;
        }
    }
}
