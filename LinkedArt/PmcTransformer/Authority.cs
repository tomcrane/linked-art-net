
using LinkedArtNet;
using PmcTransformer.Helpers;

namespace PmcTransformer
{
    public class Authority
    {
        public string? Identifier { get; set; }
        public string? Type { get; set; }
        public string? Ulan { get; set; }
        public string? Aat { get; set; }
        public string? Lux { get; set; }
        public string? Loc { get; set; }
        public string? Viaf { get; set; }
        public string? WikiData { get; set; }
        public string? Label { get; set; }

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
                laObj.Equivalent.Add(new LinkedArtObject(laObj.Type!).WithId("http://vocab.getty.edu/ulan/" + Ulan));
            }

            if (Aat.HasText())
            {
                laObj.Equivalent ??= [];
                laObj.Equivalent.Add(new LinkedArtObject(laObj.Type!).WithId("http://vocab.getty.edu/aat/" + Aat));
            }

            if (Lux.HasText())
            {
                laObj.Equivalent ??= [];
                laObj.Equivalent.Add(new LinkedArtObject(laObj.Type!).WithId(Lux));
            }

            if (Loc.HasText())
            {
                laObj.Equivalent ??= [];
                laObj.Equivalent.Add(new LinkedArtObject(laObj.Type!).WithId(Loc));
            }

            return laObj;
        }
    }
}
