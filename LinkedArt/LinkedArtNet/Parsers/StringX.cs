using System.Text;

namespace LinkedArtNet.Parsers
{
    public static class StringX
    {

        /// <summary>
        /// Only trim outer X otherwise leave
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private static string? TrimOuterX(this string? s, char x1, char x2)
        {
            if (s == null) return null;

            var s2 = s.Trim();
            if (s2.StartsWith(x1) && s2.EndsWith(x2))
            {
                var s3 = s2.Substring(1, s2.Length - 2);
                if (!s3.Contains(x1))
                {
                    return s3;
                }
            }
            return s2;
        }

        /// <summary>
        /// Only trim outer brackets otherwise leave
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string? TrimOuterBrackets(this string? s)
        {
            return TrimOuterX(s, '[', ']');
        }



        /// <summary>
        /// Only trim outer parentheses otherwise leave
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string? TrimOuterParentheses(this string? s)
        {
            return TrimOuterX(s, '(', ')');
        }

        // remove repeated spaces and convert all space to proper space char (no funny unicode spaces..)
        public static string CollapseWhiteSpace(this string s)
        {
            StringBuilder sb = new StringBuilder();
            bool prevIsSpace = false;
            foreach (char c in s)
            {
                if(char.IsWhiteSpace(c) && !prevIsSpace)
                {
                    sb.Append(' ');
                    prevIsSpace = true;
                }
                else
                {
                    sb.Append(c);
                    prevIsSpace = false;
                }
            }
            return sb.ToString();
        }

        public static string? RemoveCirca(this string? s)
        {
            if(s == null) return null;

            var s2 = s.Trim();
            var s3 = s2.Replace("circa", "").Trim();
            var len = s3.Length;
            if (len > 2)
            {
                // observed in pmc but there's a much better way to do this.
                if (s3.StartsWith('C') && char.IsDigit(s3[1]))
                {
                    return s3[1..];
                }
                if (s3.StartsWith('c') && char.IsDigit(s3[1]))
                {
                    return s3[1..];
                }
                if (s3.StartsWith("c.") && char.IsDigit(s3[2]))
                {
                    return s3[2..];
                }
                if (s3.StartsWith("c ") && char.IsDigit(s3[2]))
                {
                    return s3[2..];
                }
                if (s3.StartsWith("c. ") && char.IsDigit(s3[3]))
                {
                    return s3[3..];
                }
                if (s3.StartsWith("ca") && char.IsDigit(s3[2]))
                {
                    return s3[2..];
                }
                if (s3.StartsWith("ca.") && char.IsDigit(s3[3]))
                {
                    return s3[3..];
                }
            }
            return s3;
        }

        /// <summary>
        /// Expand things like 198- or 198? into 1980-1989
        /// or 18-- to 1800-1899
        /// 
        /// For multiple year-like strings in a phrase
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string? ExpandUncertainYears(this string? s)
        {
            if (s == null) return null;

            var parts = s.Split(' ');
            Dictionary<int, string>? partDict = null;
            for (int i = 0; i < parts.Length; i++)
            {
                string? part = parts[i];
                // yearlike: starts with at least two digits; Allow for 198? or 198-?
                if (part.Length <= 5 && part.Length > 2 && char.IsDigit(part[0]) && char.IsDigit(part[1]))
                {
                    // It's yearlike but not exactly a year!
                    string expanded = ExpandUncertainYear(part);
                    if(expanded != part)
                    {
                        partDict ??= [];
                        partDict[i] = expanded;
                    }
                }  
            }
            if(partDict == null) 
            {
                // no updates were made to string parts
                return s;
            }

            var sb = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if(partDict.ContainsKey(i))
                {
                    sb.Append(partDict[i]);
                }
                else
                {
                    sb.Append(parts[i]);
                }
                sb.Append(' ');
            }
            return sb.ToString();

        }

        /// <summary>
        /// Expand things like 198- or 198? into 1980-1989
        /// or 18-- to 1800-1899
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private static string ExpandUncertainYear(string s)
        {
            if (s.All(char.IsDigit) && (s.Length == 4 || s.Length == 3))
            {
                // actually nothing to see here
                return s;
            }
            if(s.Length == 5 && s.Take(4).All(char.IsDigit))
            {
                // ignore the last character, whatever it is
                return s[0..4];
            }
            // special case for CE years < 1000 but > 210, e.g., 850?
            // Will need updating after the year 2100
            if (s.Length == 4 && s.Take(3).All(char.IsDigit))
            {
                if (int.Parse(s[0..3]) > 210)
                {
                    return s[0..3];
                }
            }
            // dependency on yearlike logic above which should be independent
            int year = int.Parse(s[0].ToString()) * 1000 + int.Parse(s[1].ToString()) * 100;
            if (char.IsDigit(s[2]))
            {
                year += int.Parse(s[2].ToString()) * 10;
            }
            else
            {
                return $"{year}-{year + 100}"; // we will subtract 1s for our LinkedArtDateTime
            }
            return $"{year}-{year + 10}"; // we will subtract 1s for our LinkedArtDateTime
        }

        public static bool IsStartOfYear(this DateTimeOffset? dt)
        {
            if (dt.HasValue)
            {
                return dt.Value.IsStartOfYear();
            }
            return false;
        }

        public static bool IsStartOfYear(this DateTimeOffset dt)
        {
            if (dt == new DateTimeOffset(dt.Year, 1, 1, 0, 0, 0, TimeSpan.Zero))
            {
                return true;
            }
            return false;
        }
    }
}
