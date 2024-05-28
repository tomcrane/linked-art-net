using LinkedArtNet;
using LinkedArtNet.Vocabulary;

namespace Examples.NewDocExamples
{
    // https://linked.art/model/object/ownership/
    public class Ownership
    {
        public static void Create()
        {
            Spring_Ownedby_Getty();
            Nightwatch_OwnedBy_Custody();
            Nightwatch_Permanent_Custody();
            Spring_Location();
            Spring_Permanent_Location();
            Spring_Simple_Historical_Ownership();
        }


        private static void Spring_Ownedby_Getty()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/spring/6")
                .WithLabel("Jeanne (Spring) by Manet");

            spring.CurrentOwner = [
                new Group()
                    .WithId("http://vocab.getty.edu/ulan/500115988")
                    .WithLabel("Getty Museum")
            ];

            Documentation.Save(spring);
        }


        private static void Nightwatch_OwnedBy_Custody()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/nightwatch/14")
                .WithLabel("Painting")  // assume a mistake in the docs?
                .WithClassifiedAs(Getty.Painting, Getty.TypeOfWork)
                .WithClassifiedAs(Getty.Artwork);


            nightWatch.CurrentOwner = [
                new Group()
                    .WithId($"{Documentation.IdRoot}/group/amsterdam_govt")
                    .WithLabel("City of Amsterdam Governing Body")
            ];


            nightWatch.CurrentCustodian = [
                new Group()
                    .WithId("http://vocab.getty.edu/ulan/500246547")
                    .WithLabel("Rijksmuseum")
            ];

            Documentation.Save(nightWatch);
        }

        private static void Nightwatch_Permanent_Custody()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/nightwatch/17")
                .WithLabel("Painting")  // assume a mistake in the docs?
                .WithClassifiedAs(Getty.Painting, Getty.TypeOfWork)
                .WithClassifiedAs(Getty.Artwork);


            nightWatch.CurrentOwner = [
                new Group()
                    .WithId($"{Documentation.IdRoot}/group/amsterdam_govt")
                    .WithLabel("City of Amsterdam Governing Body")
            ];

            nightWatch.CurrentPermanentCustodian = new Group()
                .WithId("http://vocab.getty.edu/ulan/500246547")
                .WithLabel("Rijksmuseum");

            nightWatch.CurrentCustodian = [
                new Group()
                    .WithId("http://vocab.getty.edu/ulan/500115988")
                    .WithLabel("Getty Museum")
            ];

            Documentation.Save(nightWatch);
        }



        private static void Spring_Location()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/spring/7")
                .WithLabel("Jeanne (Spring) by Manet");

            spring.CurrentLocation = new Place()
                .WithId($"{Documentation.IdRoot}/place/W204")
                .WithLabel("Gallery W204");

            Documentation.Save(spring);
        }


        private static void Spring_Permanent_Location()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/spring/14")
                .WithLabel("Jeanne (Spring) by Manet");

            spring.CurrentPermanentLocation = new Place()
                .WithId($"{Documentation.IdRoot}/place/W204")
                .WithLabel("Gallery W204");

            spring.CurrentLocation = new Place()
                .WithId($"{Documentation.IdRoot}/place/E100")
                .WithLabel("Exhibition Gallery 100");

            Documentation.Save(spring);
        }


        private static void Spring_Simple_Historical_Ownership()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/spring/15")
                .WithLabel("Jeanne (Spring) by Manet");


            var proustAcquisition = new Activity(Types.Acquisition)
                .WithLabel("Ownership of Spring to Proust");
            proustAcquisition.TimeSpan = new LinkedArtTimeSpan
            {
                BeginOfTheBegin = new LinkedArtDate(1881, 1, 1),
                EndOfTheEnd = new LinkedArtDate(1883, 12, 31).LastSecondOfDay()
            };
            proustAcquisition.TransferredTitleFrom = [
                new Person()
                    .WithId($"{Documentation.IdRoot}/person/manet")
                    .WithLabel("Manet")
            ];
            proustAcquisition.TransferredTitleTo = [
                new Person()
                    .WithId($"{Documentation.IdRoot}/person/proust")
                    .WithLabel("Proust")
            ];
            proustAcquisition.PartOf = [
                new Activity()
                    .WithId($"{Documentation.IdRoot}/event/manet_proust")
                    .WithLabel("Full Provenance Activity")
            ];

            var payneAcquisition = new Activity(Types.Acquisition)
                .WithLabel("Ownership of Spring to Payne");
            payneAcquisition.TimeSpan = new LinkedArtTimeSpan
            {
                BeginOfTheBegin = new LinkedArtDate(1909, 1, 1),
                EndOfTheEnd = new LinkedArtDate(1909, 12, 31).LastSecondOfDay()
            };
            payneAcquisition.TransferredTitleFrom = [
                new Person()
                    .WithId($"{Documentation.IdRoot}/person/durand")
                    .WithLabel("Durand-Ruel Gallery")
            ];
            payneAcquisition.TransferredTitleTo = [
                new Person()
                    .WithId($"{Documentation.IdRoot}/person/payne")
                    .WithLabel("Oliver Payne")
            ];
            payneAcquisition.PartOf = [
                new Activity()
                    .WithId($"{Documentation.IdRoot}/event/durand_payne")
                    .WithLabel("Full Provenance Activity")
            ];

            spring.ChangedOwnershipThrough = [
                proustAcquisition,
                payneAcquisition
            ];

            Documentation.Save(spring);
        }
    }
}
