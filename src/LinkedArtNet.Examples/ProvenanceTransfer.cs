using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples
{
    /// <summary>https://linked.art/model/provenance/transfer/</summary>
    public class ProvenanceTransfer
    {
        [LinkedArtExample("provenance/transfer/1")]
        public static Activity Transfer_Of_Unknown_Kind()
        {
            var activity = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/provenance/transfer/1")
                .WithLabel("Transfer of Painting")
                .WithClassifiedAs(Aat.ProvenanceActivity);

            activity.Part = [
                new Transfer
                {
                    Transferred = [
                        new HumanMadeObject()
                            .WithId("https://linked.art/example/object/example")
                            .WithLabel("Example Painting")
                    ],
                    TransferredFrom = [
                        new Person()
                            .WithId("https://linked.art/example/person/person1")
                            .WithLabel("Person 1")
                    ],
                    TransferredTo = [
                        new Person()
                            .WithId("https://linked.art/example/person/person2")
                            .WithLabel("Person 2")
                    ]
                }
            ];

            return activity;
        }
    }
}
