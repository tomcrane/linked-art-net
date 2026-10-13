

using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples
{
    // https://linked.art/model/place/

    public class Places
    {

        [LinkedArtExample("place/amsterdam/1")]
        public static Place Amsterdam_In_Netherlands()
        {
            var amsterdam = new Place()
                .WithContext()
                .WithId("https://linked.art/example/place/amsterdam/1")
                .WithLabel("Amsterdam")
                .WithClassifiedAs(Aat.City);

            amsterdam.IdentifiedBy = [new Name("Amsterdam")];

            amsterdam.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Aat.Description, Aat.Statement)
                    .WithContent("Amsterdam is a city in the Netherlands")
            ];

            amsterdam.PartOf = [
                new Place()
                    .WithId("https://linked.art/example/place/netherlands")
                    .WithLabel("Netherlands")
                    .WithClassifiedAs(Aat.Country)
            ];

            return amsterdam;
        }


        [LinkedArtExample("place/new_zealand/1")]
        public static Place New_Zealand_Polygon()
        {
            var nz = new Place()
                .WithContext()
                .WithId("https://linked.art/example/place/new_zealand/1")
                .WithLabel("New Zealand")
                .WithClassifiedAs(Aat.Country);

            nz.DefinedBy = "POLYGON((165.74 -33.55, -179.96 -33.55, -179.96 -47.8, 165.74 -47.8, 165.74 -33.55))";

            return nz;
        }


        [LinkedArtExample("place/amsterdam_auction_house/1")]
        public static Place Christies_Amsterdam()
        {
            var christies = new Place()
                .WithContext()
                .WithId("https://linked.art/example/place/amsterdam_auction_house/1")
                .WithLabel("Christie's AMS");

            christies.IdentifiedBy = [new Name("Christie's Amsterdam Location")];

            christies.PartOf = [
                new Place()
                    .WithId("https://linked.art/example/place/amsterdam")
                    .WithLabel("Amsterdam")
            ];

            return christies;
        }


        [LinkedArtExample("object/flw_house/1")]
        public static HumanMadeObject Frank_Lloyd_Wright_House()
        {
            var house = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/flw_house/1")
                .WithLabel("Frank Lloyd Wright House")
                .WithClassifiedAs(Aat.Building);

            house.IdentifiedBy = [new Name("Frank Lloyd Wright House")];

            house.CurrentLocation = new Place()
                .WithId("https://linked.art/example/place/crystal_bridges")
                .WithLabel("Crystal Bridges");

            return house;
        }

    }
}
