using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples
{
    /// <summary>https://linked.art/model/provenance/movement/</summary>
    public class ProvenanceMovement
    {
        [LinkedArtExample("provenance/wiley_move/1")]
        public static Activity Movement_Of_Wiley_Painting()
        {
            var movement = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/provenance/wiley_move/1")
                .WithLabel("Movement of Wiley Painting")
                .WithClassifiedAs(Aat.ProvenanceActivity);

            movement.Part = [
                new Move
                {
                    Moved = [
                        new HumanMadeObject()
                            .WithId("https://linked.art/example/object/yiadom-boakye")
                            .WithLabel("Portrait of Lynette Yiadom-Boakye")
                    ],
                    MovedFrom = new Place()
                        .WithId("https://linked.art/example/place/ycba_gallery")
                        .WithLabel("YCBA Gallery"),
                    MovedTo = new Place()
                        .WithId("https://linked.art/example/place/yuag_gallery")
                        .WithLabel("YUAG Gallery")
                }
            ];

            return movement;
        }
    }
}
