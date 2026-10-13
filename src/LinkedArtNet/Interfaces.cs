namespace LinkedArtNet;

/// <summary>Classes with a <c>content</c> string: <see cref="Name"/>, <see cref="Identifier"/>, <see cref="LinguisticObject"/>.</summary>
public interface IHasContent
{
    string? Content { get; set; }
}

/// <summary>Classes with a <c>language</c> list: <see cref="Name"/>, <see cref="LinguisticObject"/>.</summary>
public interface IHasLanguage
{
    List<Language>? Language { get; set; }
}

/// <summary>Classes with a <c>dimension</c> list.</summary>
public interface IHasDimensions
{
    List<Dimension>? Dimension { get; set; }
}

/// <summary>Classes with a <c>referred_to_by</c> list of statements.</summary>
public interface IHasStatements
{
    List<LinguisticObject>? ReferredToBy { get; set; }
}
