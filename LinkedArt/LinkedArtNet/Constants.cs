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
    public static T WithClassifiedAs<T>(this T laObj, string typeId, string? typeLabel) where T : LinkedArtObject
    {
        laObj.ClassifiedAs ??= [];
        laObj.ClassifiedAs.Add(new LinkedArtObject(Types.Type) { Id = typeId, Label = typeLabel });
        return laObj;
    }

    public static T WithClassifiedAs<T>(this T laObj, LinkedArtObject typeObj) where T : LinkedArtObject
    {
        laObj.ClassifiedAs ??= [];
        laObj.ClassifiedAs.Add(typeObj);
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

    public static T AsPrimaryTitle<T>(this T laObj) where T : LinkedArtObject
    {
        laObj.ClassifiedAs ??= [];
        laObj.ClassifiedAs.Add(Getty.PrimaryTitle);
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
        laObj.Language.Add(new LinkedArtObject(Types.Language) { Id = $"{Getty.Aat}{aatCode}", Label = label });
        return laObj;
    }

    public static T WithLabel<T>(this T laObj, string label) where T : LinkedArtObject
    {
        laObj.Label = label;
        return laObj;
    }

    public static HumanMadeObject WithMadeOf(this HumanMadeObject hmo, string materialTypeId, string? materialTypeLabel)
    {
        hmo.MadeOf ??= [];
        hmo.MadeOf.Add(new LinkedArtObject(Types.Material) { Id = materialTypeId, Label = materialTypeLabel });
        return hmo;
    }
}
