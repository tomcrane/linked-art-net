using PmcTransformer.Library;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace PmcTransformer.Helpers
{
    public static partial class StringX
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

        /// <summary> 
        /// Removes separator from the start of str if it's there, otherwise leave it alone.
        /// 
        /// "something", "thing" => "something"
        /// "something", "some" => "thing"
        /// 
        /// </summary>
        /// <param name="str"></param>
        /// <param name="start"></param>
        /// <returns></returns>
        public static string? RemoveStart(this string? str, string start)
        {
            if (str == null) return null;
            if (str == string.Empty) return string.Empty;

            if (str.StartsWith(start) && str.Length > start.Length)
            {
                return str.Substring(start.Length);
            }

            return str;
        }

        public static string? ReduceGroup(this string str)
        {
            var reduced = str.Trim()
                .RemoveEnd(" Ltd").RemoveEnd(" ltd").RemoveEnd(" Limited").RemoveEnd(" limited")
                .RemoveEnd(" (Author)")
                .RemoveEnd(" Temporary")
                .RemoveStart("The ");
            return reduced;
        }

        public static string NormaliseForGroup(this string s)
        {
            var groupString = s.Trim().TrimEnd('.').Trim().TrimEnd(',').Trim();
            return groupString;
        }


        [GeneratedRegex(@"(.*) \((.*)\)")]
        private static partial Regex ThingsInParens();

        public static string RemoveThingsInParens(this string s)
        {
            var sansParens = ThingsInParens().Match(s)?.Groups.Values.Skip(1).FirstOrDefault();
            if(sansParens == null) return s;

            return sansParens.Value;
        }
    }
}
