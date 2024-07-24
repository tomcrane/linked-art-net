using System.Diagnostics.CodeAnalysis;

namespace PmcTransformer.Helpers
{
    public static class StringX
    {
        public static bool HasText([NotNullWhen(true)] this string? str) => !string.IsNullOrWhiteSpace(str);


        public static string ReplaceFirst(this string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0)
            {
                return text;
            }
            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }

        public static string? RemoveEnd(this string? str, string end)
        {
            if (str == null) return null;
            if (str == string.Empty) return string.Empty;

            if (str.EndsWith(end) && str.Length > end.Length)
            {
                return str[0..^end.Length];
            }

            return str;
        }

        public static string? ReduceGroup(this string str)
        {
            var reduced = str.Trim()
                .RemoveEnd(" Ltd").RemoveEnd(" ltd").RemoveEnd(" Limited").RemoveEnd(" limited")
                .RemoveEnd(" (Author)")
                .RemoveEnd(" Temporary");
            return reduced;
        }
    }
}
