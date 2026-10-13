using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples
{
    /// <summary>https://linked.art/model/provenance/encounters/</summary>
    public class ProvenanceEncounters
    {
        [LinkedArtExample("provenance/getty_bronze/1")]
        public static Activity Discovery_Of_Victorious_Youth()
        {
            var discovery = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/provenance/getty_bronze/1")
                .WithLabel("Discovery of Victorious Youth")
                .WithClassifiedAs(Aat.ProvenanceActivity);

            var sculpture = new HumanMadeObject()
                .WithId("https://linked.art/example/object/victorious_youth")
                .WithLabel("Victorious Youth");
            var finders = new Group()
                .WithId("https://linked.art/example/group/ferruccio")
                .WithLabel("Owners of Ferri Ferruccio");

            discovery.Part = [
                new Encounter
                {
                    Label = "Encounter of Sculpture",
                    CarriedOutBy = [finders],
                    Encountered = [sculpture]
                },
                new Acquisition
                {
                    TransferredTitleOf = [sculpture],
                    TransferredTitleTo = [finders]
                }
            ];

            return discovery;
        }
    }
}
