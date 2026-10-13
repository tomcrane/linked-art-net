using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples
{
    // https://linked.art/model/object/ownership/
    public class Ownership
    {

        [LinkedArtExample("object/spring/6")]
        public static HumanMadeObject Spring_Ownedby_Getty()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/spring/6")
                .WithLabel("Jeanne (Spring) by Manet");

            spring.CurrentOwner = [
                Ulan.Group("500115988", "Getty Museum")
            ];

            return spring;
        }


        [LinkedArtExample("object/nightwatch/14")]
        public static HumanMadeObject Nightwatch_OwnedBy_Custody()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/14")
                .WithLabel("Painting")  // assume a mistake in the docs?
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork)
                .WithClassifiedAs(Aat.Artwork);


            nightWatch.CurrentOwner = [
                new Group()
                    .WithId("https://linked.art/example/group/amsterdam_govt")
                    .WithLabel("City of Amsterdam Governing Body")
            ];


            nightWatch.CurrentCustodian = [
                Ulan.Group("500246547", "Rijksmuseum")
            ];

            return nightWatch;
        }

        [LinkedArtExample("object/nightwatch/17")]
        public static HumanMadeObject Nightwatch_Permanent_Custody()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/17")
                .WithLabel("Painting")  // assume a mistake in the docs?
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork)
                .WithClassifiedAs(Aat.Artwork);


            nightWatch.CurrentOwner = [
                new Group()
                    .WithId("https://linked.art/example/group/amsterdam_govt")
                    .WithLabel("City of Amsterdam Governing Body")
            ];

            nightWatch.CurrentPermanentCustodian = Ulan.Group("500246547", "Rijksmuseum");

            nightWatch.CurrentCustodian = [
                Ulan.Group("500115988", "Getty Museum")
            ];

            return nightWatch;
        }



        [LinkedArtExample("object/spring/7")]
        public static HumanMadeObject Spring_Location()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/spring/7")
                .WithLabel("Jeanne (Spring) by Manet");

            spring.CurrentLocation = new Place()
                .WithId("https://linked.art/example/place/W204")
                .WithLabel("Gallery W204");

            return spring;
        }


        [LinkedArtExample("object/spring/14")]
        public static HumanMadeObject Spring_Permanent_Location()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/spring/14")
                .WithLabel("Jeanne (Spring) by Manet");

            spring.CurrentPermanentLocation = new Place()
                .WithId("https://linked.art/example/place/W204")
                .WithLabel("Gallery W204");

            spring.CurrentLocation = new Place()
                .WithId("https://linked.art/example/place/E100")
                .WithLabel("Exhibition Gallery 100");

            return spring;
        }


        [LinkedArtExample("object/spring/15")]
        public static HumanMadeObject Spring_Simple_Historical_Ownership()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/spring/15")
                .WithLabel("Jeanne (Spring) by Manet");


            var proustAcquisition = new Acquisition()
                .WithLabel("Ownership of Spring to Proust");
            proustAcquisition.TimeSpan = new LinkedArtTimeSpan
            {
                BeginOfTheBegin = new LinkedArtDate(1881, 1, 1),
                EndOfTheEnd = new LinkedArtDate(1883, 12, 31).LastSecondOfDay()
            };
            proustAcquisition.TransferredTitleFrom = [
                new Person()
                    .WithId("https://linked.art/example/person/manet")
                    .WithLabel("Manet")
            ];
            proustAcquisition.TransferredTitleTo = [
                new Person()
                    .WithId("https://linked.art/example/person/proust")
                    .WithLabel("Proust")
            ];
            proustAcquisition.PartOf = [
                new Activity()
                    .WithId("https://linked.art/example/event/manet_proust")
                    .WithLabel("Full Provenance Activity")
            ];

            var payneAcquisition = new Acquisition()
                .WithLabel("Ownership of Spring to Payne");
            payneAcquisition.TimeSpan = new LinkedArtTimeSpan
            {
                BeginOfTheBegin = new LinkedArtDate(1909, 1, 1),
                EndOfTheEnd = new LinkedArtDate(1909, 12, 31).LastSecondOfDay()
            };
            payneAcquisition.TransferredTitleFrom = [
                new Person()
                    .WithId("https://linked.art/example/person/durand")
                    .WithLabel("Durand-Ruel Gallery")
            ];
            payneAcquisition.TransferredTitleTo = [
                new Person()
                    .WithId("https://linked.art/example/person/payne")
                    .WithLabel("Oliver Payne")
            ];
            payneAcquisition.PartOf = [
                new Activity()
                    .WithId("https://linked.art/example/event/durand_payne")
                    .WithLabel("Full Provenance Activity")
            ];

            spring.ChangedOwnershipThrough = [
                proustAcquisition,
                payneAcquisition
            ];

            return spring;
        }
    }
}
