
namespace PmcTransformer
{
    public static class DictX
    {
        public static void IncrementCounter(this Dictionary<int, int> dict, int key)
        {
            if (!dict.ContainsKey(key))
            {
                dict[key] = 1;
            } 
            else
            {
                dict[key]++;
            }
        }

        public static void AddToListForKey(this Dictionary<string, List<string>> dict, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }
            if (!dict.ContainsKey(key))
            {
                dict[key] = [];
            }
            dict[key].Add(value);
        }


        public static void Display(this Dictionary<int, int> dict, string message)
        {
            Console.WriteLine(message);
            foreach(var key in dict.Keys.OrderBy(k => k))
            {
                Console.WriteLine($"{key}: {dict[key]}");
            }
        }

        public static string LastPathElement(this string s)
        {
            return s.Split('/')[^1];
        }

        public static string TrimSpecial(this string s)
        {
            var s2 = s.Trim();
            if (s2.StartsWith('[') && s2.EndsWith(']'))
            {
                var s3 = s2.Substring(1, s2.Length - 2);
                if(!s3.Contains('['))
                {
                    return s3;
                }
            }
            return s2;
        }
    }
}
