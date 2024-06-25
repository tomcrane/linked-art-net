
namespace PmcTransformer
{
    public static class DictX
    {
        public static void IncrementCounter<T>(this Dictionary<T, int> dict, T key) where T : notnull
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

        public static void AddToListForKey(this Dictionary<string, List<string>> dict, string? key, string value)
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


    }
}
