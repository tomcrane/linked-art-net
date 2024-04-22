using LinkedArtNet;
using LinkedArtNet.Vocabulary;
using System.Reflection;

namespace Examples;

public class ModelObjectProduction : ExamplePage
{
    // https://linked.art/model/object/production/
    public List<HumanMadeObject> GetHumanMadeObjects()
    {
        var list = new List<HumanMadeObject>
        {
            PaintingMarch1870(),
            GlassBlowing(),
            Graffiti(),
            PaintedSculpture(),
            CopyOfPaintingOfAFish()
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
                CarriedOutBy = [new Person().WithLabel("Artist")]
            }
        ];

        return painting;
    }

    private HumanMadeObject GlassBlowing()
    {
        var glassSculpture = new HumanMadeObject()
            .WithContext()
            .WithId($"{ExamplePage.Base}{nameof(ModelObjectProduction)}/61.json")
            .WithLabel("Glass Sculpture");
        
        glassSculpture.ClassifiedAs =
        [
            Getty.Sculpture.WithClassifiedAs(Getty.TypeOfWork),
            Getty.Artwork
        ];

        glassSculpture.ProducedBy =
        [
            new Activity(Types.Production)
            {
                CarriedOutBy = [new Person().WithLabel("Glassblowing Artist")],
                Technique = [ Getty.AatType("Glassblowing", "300053932") ]
            }
        ];

        return glassSculpture;
    }


    private HumanMadeObject Graffiti()
    {
        var graffiti = new HumanMadeObject()
            .WithContext()
            .WithId($"{ExamplePage.Base}{nameof(ModelObjectProduction)}/62.json")
            .WithLabel("Graffiti")
            .WithClassifiedAs(Getty.Artwork);

        graffiti.ProducedBy =
        [
            new Activity(Types.Production)
                .WithLabel("Production of Graffiti")
                .WithClassifiedAs(Getty.AatType("Vandalism", "300055299"))
                .WithTechnique(Getty.AatType("Spraypainting", "300053816"))
        ];

        return graffiti;
    }



    private HumanMadeObject PaintedSculpture()
    {
        var paintedSculpture = new HumanMadeObject()
            .WithContext()
            .WithId($"{ExamplePage.Base}{nameof(ModelObjectProduction)}/63.json")
            .WithLabel("Painted Sculpture");

        paintedSculpture.ClassifiedAs =
        [
            Getty.Sculpture.WithClassifiedAs(Getty.TypeOfWork),
            Getty.Artwork
        ];

        var production = new Activity(Types.Production)
        {
            Part = [
                new Activity(Types.Production)
                {
                    Technique = [Getty.AatType("Sculpting", "300264383")],
                    CarriedOutBy = [new Person().WithLabel("Sculptor")]
                },
                new Activity(Types.Production)
                {
                    Technique = [Getty.AatType("Painting", "300054216")],
                    CarriedOutBy = [new Person().WithLabel("Painter")]
                }
            ]
        };

        paintedSculpture.ProducedBy = [ production ];

        return paintedSculpture;
    }


    private HumanMadeObject CopyOfPaintingOfAFish()
    {
        var fishCopy = new HumanMadeObject()
            .WithContext()
            .WithId($"{ExamplePage.Base}{nameof(ModelObjectProduction)}/64.json")
            .WithLabel("Copy of Painting of a Fish")
            .WithClassifiedAs(Getty.Painting.WithClassifiedAs(Getty.TypeOfWork))
            .WithClassifiedAs(Getty.Artwork);

        fishCopy.ProducedBy = [
            new Activity(Types.Production)
            {
                CarriedOutBy = [new Person().WithLabel("Copyist")],
                InfluencedBy = [
                    new HumanMadeObject()
                        .WithLabel("Painting of a Fish")
                        .WithClassifiedAs(Getty.Painting)
                        .WithClassifiedAs(Getty.Artwork)
                ]
            }
        ];

        return fishCopy;
    }
}
