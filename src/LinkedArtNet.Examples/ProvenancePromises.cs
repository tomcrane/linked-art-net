using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples
{
    /// <summary>https://linked.art/model/provenance/promises/</summary>
    public class ProvenancePromises
    {
        [LinkedArtExample("provenance/coffin_promise/1")]
        public static Activity Promised_Gift()
        {
            var promisedGift = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/provenance/coffin_promise/1")
                .WithLabel("Promised Gift of Coffin")
                .WithClassifiedAs(Aat.ProvenanceActivity);

            promisedGift.Part = [
                new Activity()
                    .WithLabel("Promise of Gift")
                    .WithClassifiedAs(Aat.PromiseActivity)
                    .WithStatement(Aat.Description, "Promised gift of Thomas Jaffe, B.A. 1971")
                    .WithCarriedOutBy(
                        new Person()
                            .WithId("https://linked.art/example/person/jaffe")
                            .WithLabel("Thomas Jaffe"))
                    .WithUsedSpecificObject(
                        new HumanMadeObject()
                            .WithId("https://linked.art/example/object/coffin")
                            .WithLabel("Coffin"))
                    .WithParticipant(
                        new Group()
                            .WithId("https://linked.art/example/group/yuag")
                            .WithLabel("Yale University Art Gallery"))
            ];

            return promisedGift;
        }


        [LinkedArtExample("provenance/nuveen_commission/1")]
        public static Activity Commission_Of_Painting()
        {
            var commission = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/provenance/nuveen_commission/1")
                .WithLabel("Commission of Painting")
                .WithClassifiedAs(Aat.ProvenanceActivity)
                .WithClassifiedAs(Aat.Term("300393199", "Commissioning"));

            var franke = new Person()
                .WithId("https://linked.art/example/person/franke")
                .WithLabel("Richard Franke");
            var dine = new Person()
                .WithId("https://linked.art/example/person/dine")
                .WithLabel("Jim Dine");

            commission.Part = [
                new Activity()
                    .WithLabel("Obligation to Pay Artist")
                    .WithClassifiedAs(Aat.PromiseActivity)
                    .WithStatement(Aat.Description, "Promise to pay for a painting to be created by the artist")
                    .WithCarriedOutBy(franke)
                    .WithParticipant(dine),
                new Activity()
                    .WithLabel("Obligation to Produce Painting")
                    .WithClassifiedAs(Aat.PromiseActivity)
                    .WithReferredToBy(
                        new LinguisticObject()
                            .WithClassifiedAs(Aat.Description)
                            .WithContent("Promise to produce a painting for the commissioning party"))
                    .WithCarriedOutBy(dine)
                    .WithParticipant(franke)
            ];

            return commission;
        }
    }
}
