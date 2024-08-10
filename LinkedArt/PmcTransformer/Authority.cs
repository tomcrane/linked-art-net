
using LinkedArtNet;
using PmcTransformer.Helpers;
using System.Text.Json.Serialization;

namespace PmcTransformer
{
    public class Authority
    {
        [JsonPropertyName("identifier")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Identifier { get; set; }

        [JsonPropertyName("type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Type { get; set; }

        [JsonPropertyName("ulan")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Ulan { get; set; }

        [JsonPropertyName("aat")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Aat { get; set; }

        [JsonPropertyName("lux")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Lux { get; set; }

        [JsonPropertyName("loc")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Loc { get; set; }

        [JsonPropertyName("viaf")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Viaf { get; set; }

        [JsonPropertyName("wikidata")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Wikidata { get; set; }

        [JsonPropertyName("tgn")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Tgn { get; set; }

        [JsonPropertyName("label")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Label { get; set; }

        [JsonPropertyName("pmc")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Pmc { get; set; }

        [JsonPropertyName("ignore")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Ignore { get; set; }

        /// <summary>
        /// When trying to match source strings
        /// </summary>
        [JsonIgnore]
        public int Score { get; set; }

        public bool Unreconciled { get; set; }

        public LinkedArtObject? GetReference()
        {
            LinkedArtObject? laRef = null;
            switch (Type)
            {
                case "Group":
                    laRef = new Group().WithId(Identity.GroupBase + Identifier);
                    break;
                case "Person":
                    laRef = new Person().WithId(Identity.PeopleBase + Identifier);
                    break;
                case "Place":
                    laRef = new Place().WithId(Identity.PlaceBase + Identifier);
                    break;
                case "Type": 
                case "Concept":
                    laRef = new LinkedArtObject(Types.Type).WithId(Identity.ConceptBase + Identifier);
                    break;
            }
            laRef?.WithLabel(Label);
            return laRef;
        }

        public LinkedArtObject? GetFull()
        {
            var laObj = GetReference();
            if (laObj == null) return null;

            
            if(Ulan.HasText())
            {
                laObj.Equivalent ??= [];
                laObj.Equivalent.Add(new LinkedArtObject(laObj.Type!).WithId(UlanPrefix + Ulan));
            }

            if (Aat.HasText())
            {
                laObj.Equivalent ??= [];
                laObj.Equivalent.Add(new LinkedArtObject(laObj.Type!).WithId(AatPrefix + Aat));
            }

            //if (Lux.HasText())
            //{
            //    laObj.Equivalent ??= [];
            //    laObj.Equivalent.Add(new LinkedArtObject(laObj.Type!).WithId(Lux));
            //}

            if (Loc.HasText())
            {
                laObj.Equivalent ??= [];
                var lcCategory = Type == "Concept" ? "subjects" : "names";
                laObj.Equivalent.Add(new LinkedArtObject(laObj.Type!).WithId($"{LocPrefix}{lcCategory}/{Loc}"));
            }

            if (Viaf.HasText())
            {
                laObj.Equivalent ??= [];
                laObj.Equivalent.Add(new LinkedArtObject(laObj.Type!).WithId(ViafPrefix + Viaf));
            }

            if (Wikidata.HasText())
            {
                laObj.Equivalent ??= [];
                laObj.Equivalent.Add(new LinkedArtObject(laObj.Type!).WithId(WikidataPrefix + Wikidata));
            }

            if (Pmc.HasText())
            {
                laObj.Equivalent ??= [];
                laObj.Equivalent.Add(new LinkedArtObject(laObj.Type!).WithId(Pmc));
            }

            if (Tgn.HasText())
            {
                laObj.Equivalent ??= [];
                laObj.Equivalent.Add(new LinkedArtObject(laObj.Type!).WithId(TgnPrefix + Tgn));
            }

            return laObj;
        }

        public override string ToString()
        {
            if (Label.HasText())
            {
                return Label;
            }

            if(Lux.HasText())
            {
                return Lux.Split('/')[^1];
            }

            if (Loc.HasText())
            {
                return Loc;
            }

            if (Viaf.HasText())
            {
                return "v" + Viaf;
            }

            if (Ulan.HasText())
            {
                return "u" + Ulan;
            }

            if (Tgn.HasText())
            {
                return "t" + Tgn;
            }

            return base.ToString();

        }

        public const string LocPrefix = "http://id.loc.gov/authorities/";
        public const string ViafPrefix = "http://viaf.org/viaf/";
        public const string UlanPrefix = "http://vocab.getty.edu/ulan/";
        public const string AatPrefix = "http://vocab.getty.edu/aat/";
        public const string TgnPrefix = "http://vocab.getty.edu/tgn/";
        public const string WikidataPrefix = "http://www.wikidata.org/entity/";
        public const string LuxPrefix = "https://lux.collections.yale.edu/";

    }
}
