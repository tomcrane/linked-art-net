using LinkedArtNet.Vocabulary;

namespace LinkedArtNet;

/// <summary>Fluent helpers for building Linked Art graphs. Every method returns the object it was called on.</summary>
public static partial class LinkedArtExtensions
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

    /// <summary>Sets the id; null or blank ids are ignored so optional ids can be passed through.</summary>
    public static T WithId<T>(this T laObj, string? id) where T : LinkedArtObject
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            laObj.Id = id;
        }
        return laObj;
    }

    public static T WithLabel<T>(this T laObj, string? label) where T : LinkedArtObject
    {
        laObj.Label = label;
        return laObj;
    }

    /// <summary>
    /// Adds a classification. <paramref name="metaType"/> classifies the <em>type</em> itself (e.g. Painting is a
    /// "Type of Work"), which is how Linked Art distinguishes the roles of multiple classifications.
    /// </summary>
    public static T WithClassifiedAs<T>(this T laObj, Type type, Type? metaType = null) where T : LinkedArtObject
    {
        laObj.ClassifiedAs ??= [];
        laObj.ClassifiedAs.Add(type);
        if (metaType != null)
        {
            type.WithClassifiedAs(metaType);
        }
        return laObj;
    }

    /// <summary>Adds a classification from a URI and label.</summary>
    public static T WithClassifiedAs<T>(this T laObj, string typeId, string? typeLabel, Type? metaType = null) where T : LinkedArtObject
        => laObj.WithClassifiedAs(new Type(typeId, typeLabel), metaType);

    public static T WithTechnique<T>(this T activity, Type technique) where T : ActivityBase
    {
        activity.Technique ??= [];
        activity.Technique.Add(technique);
        return activity;
    }

    public static T WithTechnique<T>(this T activity, string typeId, string? typeLabel) where T : ActivityBase
        => activity.WithTechnique(new Type(typeId, typeLabel));

    public static T AsPrimaryName<T>(this T laObj) where T : LinkedArtObject => laObj.WithClassifiedAs(Aat.PrimaryName);

    public static T AsSystemAssignedNumber<T>(this T laObj) where T : LinkedArtObject => laObj.WithClassifiedAs(Aat.SystemAssignedNumber);

    public static T AsAccessionNumber<T>(this T laObj) where T : LinkedArtObject => laObj.WithClassifiedAs(Aat.AccessionNumber);

    public static T WithContent<T>(this T laObj, string content) where T : LinkedArtObject, IHasContent
    {
        laObj.Content = content;
        return laObj;
    }

    public static T WithLanguage<T>(this T laObj, Language language) where T : LinkedArtObject, IHasLanguage
    {
        laObj.Language ??= [];
        laObj.Language.Add(language);
        return laObj;
    }

    public static T WithLanguage<T>(this T laObj, string aatCode, string label, string? notation = null) where T : LinkedArtObject, IHasLanguage
        => laObj.WithLanguage(Getty.Language(aatCode, label, notation));

    public static HumanMadeObject WithMadeOf(this HumanMadeObject hmo, string? materialTypeLabel, string materialTypeId)
    {
        if (!materialTypeId.StartsWith("http"))
        {
            materialTypeId = $"{Getty.Aat}{materialTypeId}";
        }
        hmo.MadeOf ??= [];
        hmo.MadeOf.Add(new Material(materialTypeId, materialTypeLabel));
        return hmo;
    }

    public static HumanMadeObject WithMadeOf(this HumanMadeObject hmo, Material material)
    {
        hmo.MadeOf ??= [];
        hmo.MadeOf.Add(material);
        return hmo;
    }

    /// <summary>Adds a <c>referred_to_by</c> statement: a <see cref="LinguisticObject"/> classified as the given type and as Brief Text.</summary>
    public static T WithStatement<T>(this T laObj, Type statementType, string content, Action<LinguisticObject>? configure = null)
        where T : LinkedArtObject, IHasReferredToBy
    {
        var statement = new LinguisticObject().WithContent(content).WithClassifiedAs(statementType, Aat.BriefText);
        configure?.Invoke(statement);
        laObj.ReferredToBy ??= [];
        laObj.ReferredToBy.Add(statement);
        return laObj;
    }

    /// <summary>Adds a <see cref="Name"/> to <c>identified_by</c>, optionally configuring it (e.g. <c>n => n.AsPrimaryName().WithLanguage(Aat.English)</c>).</summary>
    public static T WithName<T>(this T laObj, string content, Action<Name>? configure = null) where T : LinkedArtObject
    {
        var name = new Name(content);
        configure?.Invoke(name);
        laObj.IdentifiedBy ??= [];
        laObj.IdentifiedBy.Add(name);
        return laObj;
    }

    /// <summary>Adds an <see cref="Identifier"/> to <c>identified_by</c>, optionally configuring it (e.g. <c>i => i.AsAccessionNumber()</c>).</summary>
    public static T WithIdentifier<T>(this T laObj, string content, Action<Identifier>? configure = null) where T : LinkedArtObject
    {
        var identifier = new Identifier(content);
        configure?.Invoke(identifier);
        laObj.IdentifiedBy ??= [];
        laObj.IdentifiedBy.Add(identifier);
        return laObj;
    }

    /// <summary>Sets <c>timespan</c> to a whole year.</summary>
    public static T WithTimeSpan<T>(this T temporal, long year) where T : TemporalEntity
    {
        temporal.TimeSpan = LinkedArtTimeSpan.FromYear(year);
        return temporal;
    }

    /// <summary>Sets <c>timespan</c> to a whole day.</summary>
    public static T WithTimeSpan<T>(this T temporal, long year, int month, int day) where T : TemporalEntity
    {
        temporal.TimeSpan = LinkedArtTimeSpan.FromDay(year, month, day);
        return temporal;
    }

    /// <summary>A reference to this object: a new instance of the same class carrying only <c>id</c>, <c>type</c> and <c>_label</c>.</summary>
    public static T AsReference<T>(this T laObj) where T : LinkedArtObject
    {
        var reference = (T)(Activator.CreateInstance(laObj.GetType())
            ?? throw new InvalidOperationException($"{laObj.GetType().Name} has no parameterless constructor."));
        reference.Id = laObj.Id;
        reference.Label = laObj.Label;
        return reference;
    }
}
