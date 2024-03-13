using LinkedArtNet;
using LinkedArtNet.Vocabulary;

namespace Examples;

public class ModelObjectProduction : ExamplePage
{
    // https://linked.art/model/object/production/
    public List<HumanMadeObject> GetHumanMadeObjects()
    {
        var list = new List<HumanMadeObject>
        {
            PaintingMarch1870()
        };

        return list;
    }

    private HumanMadeObject PaintingMarch1870()
    {
        var painting = new HumanMadeObject()
            .WithContext()
            .WithId($"{ExamplePage.Base}{nameof(ModelObjectProduction)}/60.json")
            .WithLabel("Painting");

        painting.ClassifiedAs =
        [
            Getty.Painting.WithClassifiedAs(Getty.TypeOfWork),
            Getty.Artwork
        ];

        painting.ProducedBy = 
        [
            new Activity(Types.Production)
            {
                TimeSpan = [LinkedArtTimeSpan.FromDay(1780, 3, 5)],
                TookPlaceAt = [new Place().WithLabel("Artist's Studio")],
                CarriedOutBy = [new LinkedArtObject(Types.Person).WithLabel("Artist")]
            }
        ];

        return painting;
    }
}
