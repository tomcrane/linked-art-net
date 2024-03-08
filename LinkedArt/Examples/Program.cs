
using LinkedArtNet;
using System.Text.Json;


Dictionary<string, Func<HumanMadeObject>> dict = new()
{
    ["amphora"] = Amphora,
    ["amphora-dimensions"] = AmphoraWithDimensions,
    ["amphora-production"] = AmphoraProduction,
    ["amphora-work"] = AmphoraWork,
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
        .WithLanguage("en")
        .WithClassifiedAs("300404670", "Primary Title");
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
        .WithClassifiedAs("300404670", "Primary Title");
    var id = new LinkedArtObject(Types.Identifier)
        .WithId($"{amphora.Id}/id")
        .WithContent("86.AE.75")
        .WithClassifiedAs("300312355", "Accession Number");
    amphora.IdentifiedBy = [name, id];

    amphora.WithHeightDimension($"{amphora.Id}/h1", "38.7", MeasurementUnit.Centimetres);
    amphora.WithWidthDimension($"{amphora.Id}/w1", "25.7", MeasurementUnit.Centimetres);

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
        new LinkedArtObject(Types.Type)
            .WithId("icc:45A3")
            .WithLabel("Victory ~ Armed Conflict")
    ];
    amphora.Shows = [visualWork];

    return amphora;
}

