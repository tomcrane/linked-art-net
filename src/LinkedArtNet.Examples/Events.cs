using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples
{
    /// <summary>https://linked.art/model/event/</summary>
    public class Events
    {
        [LinkedArtExample("event/19c/1")]
        public static Period Nineteenth_Century()
        {
            var period = new Period()
                .WithContext()
                .WithId("https://linked.art/example/event/19c/1")
                .WithLabel("19th Century");

            period.IdentifiedBy = [new Name("19th Century").AsPrimaryName()];
            period.TimeSpan = LinkedArtTimeSpan.FromYears(1800, 1899);

            return period;
        }


        [LinkedArtExample("event/early_roman/1")]
        public static Period Early_Roman_Empire()
        {
            var period = new Period()
                .WithContext()
                .WithId("https://linked.art/example/event/early_roman/1")
                .WithLabel("Early Roman Empire (31 BCE - 193 CE)");

            period.IdentifiedBy = [new Name("Early Roman Empire").AsPrimaryName()];

            period.PartOf = [
                new Period()
                    .WithId("https://linked.art/example/event/roman")
                    .WithLabel("Roman Empire")
            ];

            return period;
        }


        [LinkedArtExample("event/vesuvius/1")]
        public static Event Eruption_Of_Vesuvius()
        {
            var eruption = new Event()
                .WithContext()
                .WithId("https://linked.art/example/event/vesuvius/1")
                .WithLabel("Eruption of Vesuvius");

            eruption.IdentifiedBy = [new Name("Eruption of Vesuvius").AsPrimaryName()];

            eruption.TimeSpan = new LinkedArtTimeSpan
            {
                BeginOfTheBegin = new LinkedArtDate(79, 8, 24, 12, 0, 0),
                EndOfTheEnd = new LinkedArtDate(79, 8, 26).EndOfDay()
            };

            eruption.TookPlaceAt = [
                new Place()
                    .WithId("https://linked.art/example/place/vesuvius")
                    .WithLabel("Mount Vesuvius")
            ];

            return eruption;
        }


        [LinkedArtExample("event/vesuvius/2")]
        public static Event Eruption_During_Period()
        {
            var eruption = new Event()
                .WithContext()
                .WithId("https://linked.art/example/event/vesuvius/2")
                .WithLabel("Eruption of Vesuvius");

            eruption.IdentifiedBy = [new Name("Eruption of Vesuvius").AsPrimaryName()];

            eruption.During = [
                new Period()
                    .WithId("https://linked.art/example/event/early_roman")
                    .WithLabel("Early Roman Empire")
            ];

            return eruption;
        }


        [LinkedArtExample("object/pompeii-buildings/1")]
        public static HumanMadeObject Pompeii_Destroyed_By_Eruption()
        {
            var pompeii = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/pompeii-buildings/1")
                .WithLabel("Buildings making up the city of Pompeii");

            pompeii.IdentifiedBy = [new Name("City of Pompeii").AsPrimaryName()];

            pompeii.DestroyedBy = new Destruction
            {
                CausedBy = [
                    new Event().WithId("https://linked.art/example/event/vesuvius")
                ]
            };

            return pompeii;
        }


        [LinkedArtExample("object/pompeii_statue/1")]
        public static HumanMadeObject Statue_Produced_Before_Eruption()
        {
            var statue = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/pompeii_statue/1")
                .WithLabel("Statue in Pompeii");

            statue.IdentifiedBy = [new Name("Pompeii Statue").AsPrimaryName()];

            statue.ProducedBy = new Production
            {
                Before = [
                    new Event()
                        .WithId("https://linked.art/example/event/vesuvius")
                        .WithLabel("Eruption of Vesuvius")
                ]
            };

            return statue;
        }
    }
}
