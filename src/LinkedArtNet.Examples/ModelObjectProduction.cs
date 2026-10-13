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
            Aat.Painting.WithClassifiedAs(Aat.TypeOfWork),
            Aat.Artwork
        ];

        painting.ProducedBy =
            new Production()
            {
                TimeSpan = LinkedArtTimeSpan.FromDay(1780, 3, 5),
                TookPlaceAt = [new Place().WithLabel("Artist's Studio")],
                CarriedOutBy = [new Person().WithLabel("Artist")]
            };

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
            Aat.Sculpture.WithClassifiedAs(Aat.TypeOfWork),
            Aat.Artwork
        ];

        glassSculpture.ProducedBy =
            new Production()
            {
                CarriedOutBy = [new Person().WithLabel("Glassblowing Artist")],
                Technique = [Aat.Glassblowing]
            };

        return glassSculpture;
    }


    private HumanMadeObject Graffiti()
    {
        var graffiti = new HumanMadeObject()
            .WithContext()
            .WithId($"{ExamplePage.Base}{nameof(ModelObjectProduction)}/62.json")
            .WithLabel("Graffiti")
            .WithClassifiedAs(Aat.Artwork);

        graffiti.ProducedBy =
            new Production()
                .WithLabel("Production of Graffiti")
                .WithClassifiedAs(Aat.Vandalism)
                .WithTechnique(Aat.Spraypainting);

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
            Aat.Sculpture.WithClassifiedAs(Aat.TypeOfWork),
            Aat.Artwork
        ];

        var production = new Production()
        {
            Part = [
                new Production()
                {
                    Technique = [Aat.Sculpting],
                    CarriedOutBy = [new Person().WithLabel("Sculptor")]
                },
                new Production()
                {
                    Technique = [Aat.PaintingTechnique],
                    CarriedOutBy = [new Person().WithLabel("Painter")]
                }
            ]
        };

        paintedSculpture.ProducedBy = production;

        return paintedSculpture;
    }


    private HumanMadeObject CopyOfPaintingOfAFish()
    {
        var fishCopy = new HumanMadeObject()
            .WithContext()
            .WithId($"{ExamplePage.Base}{nameof(ModelObjectProduction)}/64.json")
            .WithLabel("Copy of Painting of a Fish")
            .WithClassifiedAs(Aat.Painting.WithClassifiedAs(Aat.TypeOfWork))
            .WithClassifiedAs(Aat.Artwork);

        fishCopy.ProducedBy =
            new Production()
            {
                CarriedOutBy = [new Person().WithLabel("Copyist")],
                InfluencedBy = [
                    new HumanMadeObject()
                        .WithLabel("Painting of a Fish")
                        .WithClassifiedAs(Aat.Painting)
                        .WithClassifiedAs(Aat.Artwork)
                ]
            };

        return fishCopy;
    }
}
