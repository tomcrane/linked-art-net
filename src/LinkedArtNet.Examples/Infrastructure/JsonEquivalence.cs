using System.Text.Json;

namespace LinkedArtNet.Examples;

/// <summary>
/// Structural JSON comparison: object property order is ignored, array order is significant, numbers compare
/// by value. Returns a JSON-pointer-ish path to the first difference, or null when equivalent.
/// </summary>
public static class JsonEquivalence
{
    public static bool AreEquivalent(string expectedJson, string actualJson) => FirstDifference(expectedJson, actualJson) is null;

    public static string? FirstDifference(string expectedJson, string actualJson)
    {
        using var expected = JsonDocument.Parse(expectedJson);
        using var actual = JsonDocument.Parse(actualJson);
        return Compare(expected.RootElement, actual.RootElement, "");
    }

    private static string? Compare(JsonElement expected, JsonElement actual, string path)
    {
        if (expected.ValueKind != actual.ValueKind)
            return $"{path}: expected {expected.ValueKind}, actual {actual.ValueKind}";

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                var expectedProps = expected.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
                var actualProps = actual.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
                foreach (var name in expectedProps.Keys.Except(actualProps.Keys))
                    return $"{path}/{name}: missing from actual";
                foreach (var name in actualProps.Keys.Except(expectedProps.Keys))
                    return $"{path}/{name}: unexpected in actual";
                foreach (var (name, value) in expectedProps)
                {
                    if (Compare(value, actualProps[name], $"{path}/{name}") is { } diff) return diff;
                }
                return null;

            case JsonValueKind.Array:
                var e = expected.EnumerateArray().ToList();
                var a = actual.EnumerateArray().ToList();
                if (e.Count != a.Count) return $"{path}: expected {e.Count} items, actual {a.Count}";
                for (var i = 0; i < e.Count; i++)
                {
                    if (Compare(e[i], a[i], $"{path}/{i}") is { } diff) return diff;
                }
                return null;

            case JsonValueKind.Number:
                return expected.GetDouble() == actual.GetDouble() ? null : $"{path}: expected {expected.GetRawText()}, actual {actual.GetRawText()}";

            case JsonValueKind.String:
                return expected.GetString() == actual.GetString() ? null : $"{path}: expected \"{expected.GetString()}\", actual \"{actual.GetString()}\"";

            default:
                return null; // true/false/null: kinds already matched
        }
    }
}
