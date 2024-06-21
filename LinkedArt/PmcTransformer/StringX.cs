using System.Diagnostics.CodeAnalysis;

namespace PmcTransformer
{
    public static class StringX
    {
        public static bool HasText([NotNullWhen(true)] this string? str) => !string.IsNullOrWhiteSpace(str);
    }
}
