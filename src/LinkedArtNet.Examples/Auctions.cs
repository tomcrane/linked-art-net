using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples
{
    /// <summary>https://linked.art/model/provenance/auctions/</summary>
    public class Auctions
    {
        [LinkedArtExample("event/christies_24477/1")]
        public static Activity Auction_Event()
        {
            var auction = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/event/christies_24477/1")
                .WithLabel("Impressionist Sale")
                .WithClassifiedAs(Aat.AuctionEvent);

            auction.IdentifiedBy = [new Name("Impressionist & Modern Evening Sale").AsPrimaryName()];

            auction.TimeSpan = new LinkedArtTimeSpan
            {
                Label = "Nov 5, 2014",
                BeginOfTheBegin = new LinkedArtDate(2014, 11, 5, 17, 0, 0),
                EndOfTheEnd = new LinkedArtDate(2014, 11, 5).EndOfDay()
            };

            auction.TookPlaceAt = [
                new Place()
                    .WithId("https://linked.art/example/place/christies_nyc")
                    .WithLabel("Christie's, New York")
                    .WithClassifiedAs(Aat.Term("300005234", "Auction House (place)"))
            ];

            auction.CarriedOutBy = [
                new Group()
                    .WithId("https://linked.art/example/group/christies")
                    .WithLabel("Christie's")
                    .WithClassifiedAs(Aat.Term("300417515", "Auction House (organization)"))
            ];

            return auction;
        }


        [LinkedArtExample("set/24477_16_objects/1")]
        public static Set Auction_Lot()
        {
            var lot = new Set()
                .WithContext()
                .WithId("https://linked.art/example/set/24477_16_objects/1")
                .WithLabel("Set of Objects for Lot 16")
                .WithClassifiedAs(Aat.Term("300411307", "Auction Lot"));

            lot.IdentifiedBy = [
                new Name("Lot 16: Le Printemps").AsPrimaryName(),
                new Identifier("16").WithClassifiedAs(Aat.AuctionLotNumber)
            ];

            return lot;
        }


        [LinkedArtExample("object/spring/20")]
        public static HumanMadeObject Spring_In_Lot()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/spring/20")
                .WithLabel("Spring")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

            spring.IdentifiedBy = [new Name("Jeanne (Spring)").AsPrimaryName()];

            spring.MemberOf = [
                new Set()
                    .WithId("https://linked.art/example/set/24477_16_objects")
                    .WithLabel("Lot 16: Le Printemps")
            ];

            return spring;
        }


        [LinkedArtExample("set/24477_16_objects/2")]
        public static Set Lot_Estimated_Price()
        {
            var lot = new Set()
                .WithContext()
                .WithId("https://linked.art/example/set/24477_16_objects/2")
                .WithLabel("Set of Objects for Lot 16")
                .WithClassifiedAs(Aat.Term("300411307", "Auction Lot"));

            lot.Dimension = [
                new MonetaryAmount
                {
                    Value = 25000000,
                    Currency = Aat.UsDollars.WithNotation("USD")
                }.WithClassifiedAs(Aat.Term("300417244", "Estimated Price"))
            ];

            return lot;
        }


        [LinkedArtExample("event/christies_24477_16/1")]
        public static Activity Auction_Of_Lot()
        {
            var auctionOfLot = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/event/christies_24477_16/1")
                .WithLabel("Auction of Lot 16")
                .WithClassifiedAs(Aat.Term("300420001", "Auction of Lot"));

            auctionOfLot.IdentifiedBy = [new Name("Auction of Lot 16").AsPrimaryName()];

            auctionOfLot.UsedSpecificObject = [
                new Set()
                    .WithId("https://linked.art/example/set/24477_16_objects")
                    .WithLabel("Lot 16")
            ];

            auctionOfLot.PartOf = [
                new Activity().WithId("https://linked.art/example/event/christies_24477")
            ];

            return auctionOfLot;
        }


        [LinkedArtExample("provenance/payneheirs_getty/2")]
        public static Activity Purchase_With_Custody_To_Department()
        {
            var purchase = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/provenance/payneheirs_getty/2")
                .WithLabel("Purchase of Spring")
                .WithClassifiedAs(Aat.ProvenanceActivity);

            var spring = new HumanMadeObject()
                .WithId("https://linked.art/example/object/spring")
                .WithLabel("Spring");
            var payneHeirs = new Group()
                .WithId("https://linked.art/example/group/payne_heirs")
                .WithLabel("Family of Oliver Payne");

            purchase.Part = [
                new Acquisition
                {
                    Label = "Acquisition of Painting",
                    TransferredTitleOf = [spring],
                    TransferredTitleFrom = [payneHeirs],
                    TransferredTitleTo = [
                        new Group()
                            .WithId("https://linked.art/example/group/getty")
                            .WithLabel("J. Paul Getty Museum")
                    ]
                },
                new TransferOfCustody
                {
                    Label = "Custody by Department",
                    TransferredCustodyOf = [spring],
                    TransferredCustodyFrom = [payneHeirs],
                    TransferredCustodyTo = [
                        new Group()
                            .WithId("https://linked.art/example/group/getty_ptgs")
                            .WithLabel("Paintings Department")
                    ]
                }
            ];

            return purchase;
        }


        [LinkedArtExample("text/catalog_24477/1")]
        public static LinguisticObject Auction_Catalog()
        {
            var catalog = new LinguisticObject()
                .WithContext()
                .WithId("https://linked.art/example/text/catalog_24477/1")
                .WithLabel("Auction Catalog")
                .WithClassifiedAs(Aat.AuctionCatalog, Aat.TypeOfWork);

            catalog.IdentifiedBy = [new Name("Impressionist & Modern Evening Sale Catalog").AsPrimaryName()];

            catalog.About = [
                new Activity()
                    .WithId("https://linked.art/example/event/christies_24477")
                    .WithLabel("Impressionist Sale")
            ];

            return catalog;
        }
    }
}
