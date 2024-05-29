using LinkedArtNet.Vocabulary;
using System;

namespace LinkedArtNet;

public static class Constants
{
    public static readonly string[] Context = ["https://linked.art/ns/v1/linked-art.json"];

    public static T WithContext<T>(this T laObj) where T : LinkedArtObject
    {
        if (laObj.Context == null)
        {
            laObj.Context = Context;
            return laObj;
        }
        if (laObj.Context.Contains(Context[0]))
        {
            return laObj;
        }
        laObj.Context = [.. laObj.Context, .. Context];
        return laObj;
    }

    public static T WithId<T>(this T laObj, string? id) where T : LinkedArtObject
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            laObj.Id = id;
        }
        return laObj;
    }
    public static T WithClassifiedAs<T>(this T laObj, 
        string typeId, string? typeLabel, 
        LinkedArtObject? furtherClassifiedAs = null) where T : LinkedArtObject
    {
        var typeObj = new LinkedArtObject(Types.Type) { Id = typeId, Label = typeLabel };
        laObj.WithClassifiedAs(typeObj, furtherClassifiedAs);
        return laObj;
    }

    public static T WithClassifiedAs<T>(
        this T laObj, 
        LinkedArtObject typeObj, 
        LinkedArtObject? furtherClassifiedAs = null) where T : LinkedArtObject
    {
        laObj.ClassifiedAs ??= [];
        laObj.ClassifiedAs.Add(typeObj);
        if(furtherClassifiedAs != null)
        {
            typeObj.WithClassifiedAs(furtherClassifiedAs);
        }
        return laObj;
    }

    public static Activity WithTechnique(this Activity activity, string typeId, string? typeLabel)
    {
        activity.Technique ??= [];
        activity.Technique.Add(new LinkedArtObject(Types.Type) { Id = typeId, Label = typeLabel });
        return activity;
    }

    public static Activity WithTechnique(this Activity activity, LinkedArtObject typeObj)
    {
        activity.Technique ??= [];
        activity.Technique.Add(typeObj);
        return activity;
    }

    public static T AsPrimaryName<T>(this T laObj) where T : LinkedArtObject
    {
        laObj.ClassifiedAs ??= [];
        laObj.ClassifiedAs.Add(Getty.PrimaryName);
        return laObj;
    }


    public static T WithContent<T>(this T laObj, string content) where T : LinkedArtObject
    {
        laObj.Content = content;
        return laObj;
    }

    public static T WithLanguage<T>(this T laObj, string aatCode, string label) where T : LinkedArtObject
    {
        laObj.Language ??= [];
        laObj.Language.Add(Getty.Language(aatCode, label));
        return laObj;
    }

    public static T WithLabel<T>(this T laObj, string label) where T : LinkedArtObject
    {
        laObj.Label = label;
        return laObj;
    }

    public static HumanMadeObject WithMadeOf(this HumanMadeObject hmo, string? materialTypeLabel, string materialTypeId)
    {
        if(!materialTypeId.StartsWith("http"))
        {
            materialTypeId = $"{Getty.Aat}{materialTypeId}";
        }
        hmo.MadeOf ??= [];
        hmo.MadeOf.Add(new LinkedArtObject(Types.Material) { Id = materialTypeId, Label = materialTypeLabel });
        return hmo;
    }
}
