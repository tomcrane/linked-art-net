﻿
using Examples;
using LinkedArtNet;
using System.Text.Json;
using System.Text.RegularExpressions;


Dictionary<string, Func<HumanMadeObject>> dict = new()
{
    ["amphora"] = Amphora,
    ["amphora-dimensions"] = AmphoraWithDimensions,
    ["amphora-production"] = AmphoraProduction,
    ["amphora-work"] = AmphoraWork,
    ["stieglitz"] = PortraitOfKatherineStieglitz
};


static void Make(string key, Dictionary<string, Func<HumanMadeObject>> dict)
{
    var options = new JsonSerializerOptions { WriteIndented = true };
    var laObj = dict[key.ToLowerInvariant()]();
    var json = JsonSerializer.Serialize(laObj, options);
    Console.WriteLine(json);
    File.WriteAllText($"../../../output/{key}.json", json);
}


if (args.Length == 1 && dict.ContainsKey(args[0].ToLowerInvariant()))
{
    var key = args[0].ToLowerInvariant();
    Make(key, dict);
}
else if(args.Length == 2 && args[0] == "examples")
{
    var options = new JsonSerializerOptions { WriteIndented = true };
    var type = Type.GetType($"Examples.{args[1]}, Examples");
    var examplePage = Activator.CreateInstance(type!) as ExamplePage;
    if(examplePage != null)
    {
        foreach(var example in examplePage.GetHumanMadeObjects())
        {
            var filename = example!.Id!.Split("/").Last();
            var json = JsonSerializer.Serialize(example, options);
            Console.WriteLine(json);
            string directory = $"../../../output/{args[1]}/";
            Directory.CreateDirectory(directory);
            File.WriteAllText($"{directory}{filename}", json);
        }
    }
}
else
{
    foreach(var key in dict.Keys)
    {
        Make(key, dict);
    }
}

HumanMadeObject Amphora()
{
    var amphora = new HumanMadeObject()
        .WithContext()
        .WithId("https://example.org/11733")
        .WithClassifiedAs("300148696", "Amphora");

    var name = new LinkedArtObject(Types.Name)
        .WithId($"{amphora.Id}/name1")
        .WithContent("Attic Black Figure Neck Amphora")
        .WithLanguage("300388277", "English")
        .AsPrimaryTitle();
    var id = new LinkedArtObject(Types.Identifier)
        .WithId($"{amphora.Id}/id")
        .WithContent("86.AE.75")
        .WithClassifiedAs("300312355", "Accession Number");
    amphora.IdentifiedBy = [name, id];

    var desc = new LinkedArtObject(Types.LinguisticObject)
        .WithId($"{amphora.Id}/desc1")
        .WithContent("38.7 x 26.7 cm")
        .WithClassifiedAs("300435430", "Dimension Statement");
    amphora.ReferredToBy = [desc];

    return amphora;
}

HumanMadeObject AmphoraWithDimensions()
{
    var amphora = new HumanMadeObject()
        .WithContext()
        .WithId("https://example.org/11733")
        .WithClassifiedAs("300148696", "Amphora");

    var name = new LinkedArtObject(Types.Name)
        .WithId($"{amphora.Id}/name1")
        .WithContent("Attic Black Figure Neck Amphora")
        .AsPrimaryTitle();
    var id = new LinkedArtObject(Types.Identifier)
        .WithId($"{amphora.Id}/id")
        .WithContent("86.AE.75")
        .WithClassifiedAs("300312355", "Accession Number");
    amphora.IdentifiedBy = [name, id];

    amphora.WithHeightDimension($"{amphora.Id}/h1", 38.7, MeasurementUnit.Centimetres);
    amphora.WithWidthDimension($"{amphora.Id}/w1", 25.7, MeasurementUnit.Centimetres);

    return amphora;
}


HumanMadeObject AmphoraProduction()
{
    var amphora = new HumanMadeObject()
        .WithContext()
        .WithId("https://example.org/11733")
        .WithClassifiedAs("300148696", "Amphora");

    // omit some info for brevity

    var production = new Activity(Types.Production)
        .WithId($"{amphora.Id}/prod");
    
    var affecter = new LinkedArtObject(Types.Person)
        .WithId("500029118")
        .WithLabel("Affecter");
    production.CarriedOutBy = [affecter];

    var athens = new Place()
        .WithId("7001393")
        .WithLabel("Athens, Greece");
    production.TookPlaceAt = [athens];

    var when = new LinkedArtTimeSpan()
        .WithId($"{amphora.Id}/prod/ts")
        .WithLabel("about 530 BC");
    when.BeginOfTheBegin = new LinkedArtDate(-540, 1, 1);
    when.EndOfTheEnd = new LinkedArtDate(-520, 1, 1);
    // For AD dates:
    //when.BeginOfTheBegin = new LinkedArtDate(DateTime.Now);
    //when.EndOfTheEnd = new LinkedArtDate(DateTime.Now.AddDays(20));
    production.TimeSpan = [when];

    amphora.ProducedBy = [production];

    return amphora;
}

HumanMadeObject AmphoraWork()
{
    var amphora = new HumanMadeObject()
        .WithContext()
        .WithId("https://example.org/11733")
        .WithClassifiedAs("300148696", "Amphora");

    // omit some info for brevity
    var visualWork = new Work(Types.VisualItem);
    visualWork.Represents = [
        new LinkedArtObject(Types.Person)
            .WithId("icc:94M")
            .WithLabel("Theseus")
    ];
    visualWork.About = [
        new LinkedArtObject(Types.Concept)
            .WithId("icc:45A3")
            .WithLabel("Victory ~ Armed Conflict")
    ];
    amphora.Shows = [visualWork];

    return amphora;
}



HumanMadeObject PortraitOfKatherineStieglitz()
{
    // This follows the way the graph is built up in the video
    // https://youtu.be/afO7KEysda8

    // https://www.rijksmuseum.nl/en/collection/RP-F-F17653
    var photograph = new HumanMadeObject()
        .WithContext()
        .WithId("https://www.rijksmuseum.nl/en/collection/RP-F-F17653") // actually the web page but use for now
        .WithLabel("Portret van Katherine Stieglitz");

    var nameDutch = new LinkedArtObject(Types.Name)
        .WithId($"{photograph.Id}/name-nl")
        .WithContent("Portret van Katherine Stieglitz")
        .WithLanguage("300388277", "Dutch")
        .AsPrimaryTitle();
    var nameEnglish = new LinkedArtObject(Types.Name)
        .WithId($"{photograph.Id}/name-en")
        .WithContent("Portrait of Katherine Stieglitz")
        .WithLanguage("300388277", "English")
        .AsPrimaryTitle();

    photograph.IdentifiedBy = [nameDutch, nameEnglish];

    photograph.WithClassifiedAs("aat:Photograph", "Photograph");


    var identifier = new LinkedArtObject(Types.Identifier)
        .WithId($"{photograph.Id}/id")
        .WithContent("RP-F-F17653")
        .WithClassifiedAs("aat:300312355", "Accession Number");
    var rijksmuseum = new LinkedArtObject(Types.Group) // tbc
        .WithId("https://www.rijksmuseum.nl/")
        .WithLabel("Rijksmuseum");
    var assignment = new Activity(Types.AttributeAssignment) // tbc
        .WithId($"{photograph.Id}/id/attribAssign");
    assignment.CarriedOutBy = [rijksmuseum];
    identifier.AssignedBy = [assignment];
    photograph.IdentifiedBy.Add(identifier);


    var production = new Activity(Types.Production)
        .WithId($"{photograph.Id}/prod")
        .WithClassifiedAs("aat:Printing", "Negative printing");        
    var stieglitz = new LinkedArtObject(Types.Person)
        .WithId("https://www.wikidata.org/wiki/Q313055") // yes but no but
        .WithLabel("Alfred Stieglitz");
    production.CarriedOutBy = [stieglitz];
    // Included for clarity following along with video
    production.UsedSpecificObject = [
        new HumanMadeObject()
            .WithId("https://stieglitz.org/neg-of-portret-van-ks")
            .WithLabel("Negative of Portret van Katherine Stieglitz")
    ];
    production.TimeSpan = [LinkedArtTimeSpan.FromYear(1905, $"{photograph.Id}/ts")];
    photograph.ProducedBy = [production];

    photograph.WithMadeOf("aat:Paper", "Paper");


    var desc = new LinkedArtObject(Types.LinguisticObject)
        .WithId($"{photograph.Id}/desc1")
        .WithLanguage("300388277", "English")
        .WithContent("h 302 mm x w 210 mm")
        .WithClassifiedAs("300435430", "Measurement Statement");
    desc.ClassifiedAs[0].WithClassifiedAs("la-tot", "Type of Text");
    photograph.ReferredToBy = [desc];

    // make this numers npt strings
    photograph.WithHeightDimension($"{photograph.Id}/h1", 302, MeasurementUnit.Millimetres);
    photograph.WithWidthDimension($"{photograph.Id}/w1", 210, MeasurementUnit.Millimetres);

    // digression into order, using alternate name:
    var nameAlternate = new LinkedArtObject(Types.Name)
        .WithId($"{photograph.Id}/name-alt")
        .WithContent("Portrait of daughter of Stieglitz")
        .WithLanguage("300388277", "English")
        .WithClassifiedAs("300404671", "Alternate Title");
    photograph.IdentifiedBy.Add(nameAlternate);



    var visualWork = new Work(Types.VisualItem); // Is this a work?
    visualWork.Represents = [
        new LinkedArtObject(Types.Person)
            .WithId("icc:61bb2")
            .WithLabel("Katherine Stieglitz")
    ];
    visualWork.About = [
        new LinkedArtObject(Types.Concept)
            .WithId("icc:Woman")
            .WithLabel("Woman")
    ];
    photograph.Shows = [visualWork];

    // As the relationship is from provenance to object, we won't see this bit in the serialisation
    var acquisition = new Activity(Types.Acquisition)
        .WithId($"{photograph.Id}/acquisition");
    acquisition.TransferredTitleOf = [photograph];
    acquisition.TransferredTitleTo = [rijksmuseum];
    acquisition.TimeSpan = [LinkedArtTimeSpan.FromYear(1994, $"{acquisition.Id}/ts")];
    var provenance = new Activity(Types.Provenance)
        .WithId($"{photograph.Id}/provenance")
        .WithClassifiedAs("aat:Provenance", "Provenance");
    provenance.Part = [acquisition];

    // omit discussion of the Frame

    return photograph;
}
