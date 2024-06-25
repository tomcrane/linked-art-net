﻿using System.Diagnostics.CodeAnalysis;

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
    }
}
