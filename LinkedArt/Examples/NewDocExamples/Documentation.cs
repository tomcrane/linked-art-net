using LinkedArtNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Examples.NewDocExamples
{
    public class Documentation
    {
        static JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true };

        public static void Create()
        {
            BasicPatterns.Create();

        }

        internal static void Save<T>(T laObj) where T : LinkedArtObject
        {
            var fi = new FileInfo(laObj!.Id!.Replace(IdRoot, $"../../../output/new/") + ".json");
            var json = JsonSerializer.Serialize(laObj, options);
            Console.WriteLine(json);
            Directory.CreateDirectory(fi.DirectoryName!);
            File.WriteAllText(fi.FullName, json);
        }

        public const string IdRoot = "https://linked.art/example";
    }
}
