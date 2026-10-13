namespace LinkedArtNet.Examples;

/// <summary>
/// Marks a static, parameterless method that rebuilds one example published on linked.art. <paramref name="id"/> is the
/// path under <c>https://linked.art/example/</c>, for example <c>object/spring/1</c>; the method returns the object
/// whose <c>id</c> is that URI. <see cref="ExampleCatalog"/> discovers the methods; the examples tests compare each
/// method's JSON with the site's.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LinkedArtExampleAttribute(string id) : Attribute
{
    public string Id { get; } = id;
}

/// <summary>
/// Records that an example cannot match the site because of a problem on the site (a duplicated id, invalid JSON, a
/// discrepancy with the 1.0 schema). The tests expect a mismatch and report it instead of failing; <see cref="Url"/>
/// links the upstream issue once filed. We never bend the generated output to reproduce a site bug.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class SiteIssueAttribute(string reason) : Attribute
{
    public string Reason { get; } = reason;

    /// <summary>The linked.art issue, when one has been filed.</summary>
    public string? Url { get; init; }
}
