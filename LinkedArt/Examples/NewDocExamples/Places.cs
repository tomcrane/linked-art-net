

using LinkedArtNet;
using LinkedArtNet.Vocabulary;

namespace Examples.NewDocExamples
{
    // https://linked.art/model/place/

    public class Places
    {
        public static void Create()
        {
            Amsterdam_In_Netherlands();
            New_Zealand_Polygon();
            Christies_Amsterdam();
            Frank_Lloyd_Wright_House();
        }


        private static void Amsterdam_In_Netherlands()
        {
            var amsterdam = new Place()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/place/amsterdam/1")
                .WithLabel("Amsterdam")
                .WithClassifiedAs(Getty.City);

            amsterdam.IdentifiedBy = [new Name("Amsterdam")];

            amsterdam.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Getty.Description, Getty.BriefText)
                    .WithContent("Amsterdam is a city in the Netherlands")
            ];

            amsterdam.PartOf = [
                new Place()
                    .WithId($"{Documentation.IdRoot}/place/netherlands")
                    .WithLabel("Netherlands")
                    .WithClassifiedAs(Getty.Nation)
            ];

            Documentation.Save(amsterdam);
        }


        private static void New_Zealand_Polygon()
        {
            var nz = new Place()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/place/new_zealand/1")
                .WithLabel("New Zealand")
                .WithClassifiedAs(Getty.Nation);

            nz.DefinedBy = "POLYGON((165.74 -33.55, -179.96 -33.55, -179.96 -47.8, 165.74 -47.8, 165.74 -33.55))";

            Documentation.Save(nz);
        }


        private static void Christies_Amsterdam()
        {
            var christies = new Place()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/place/amsterdam_auction_house/1")
                .WithLabel("Christie's AMS");

            christies.IdentifiedBy = [new Name("Christie's Amsterdam Location")];

            christies.PartOf = [
                new Place()
                    .WithId($"{Documentation.IdRoot}/place/amsterdam")
                    .WithLabel("Amsterdam")
            ];

            Documentation.Save (christies);
        }


        private static void Frank_Lloyd_Wright_House()
        {
            var house = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/flw_house/1")
                .WithLabel("Frank Lloyd Wright House")
                .WithClassifiedAs(Getty.Building);

            house.IdentifiedBy = [new Name("Frank Lloyd Wright House")];

            house.CurrentLocation = new Place()
                .WithId($"{Documentation.IdRoot}/place/crystal_bridges")
                .WithLabel("Crystal Bridges");

            Documentation.Save(house);
        }

    }
}
