using LateApexEarlySpeed.Json.Schema.FluentGenerator;
using LateApexEarlySpeed.Xunit.Assertion.Json;
using LinkedArtNet;
using System.Text.Json;

namespace Examples.NewDocExamples
{
    public class Documentation
    {
        static JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true };
        static HttpClient httpClient = new HttpClient();

        public static void Create()
        {
            BasicPatterns.Create();
            ProductionDestruction.Create();
            PhysicalCharacteristics.Create();

        }

        internal static void Save<T>(T laObj, bool validateAgainstLive = true) where T : LinkedArtObject
        {
            var generatedFile = new FileInfo(laObj!.Id!.Replace(IdRoot, $"../../../output/new/") + ".json");
            var cachedFromLASiteFile = new FileInfo(laObj!.Id!.Replace(IdRoot, $"../../../cached/new/") + ".json");
            var generatedJson = JsonSerializer.Serialize(laObj, options);
            Console.WriteLine(generatedJson);
            Directory.CreateDirectory(generatedFile.DirectoryName!);
            File.WriteAllText(generatedFile.FullName, generatedJson);

            if(!validateAgainstLive)
            {
                return;
            }
            if(!File.Exists(cachedFromLASiteFile.FullName))
            {
                // This is very verbose when synchronous!
                var resp = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, laObj.Id + ".json"));
                resp.EnsureSuccessStatusCode();
                var stream = resp.Content.ReadAsStream();
                var laSiteJson = new StreamReader(stream).ReadToEnd();
                Directory.CreateDirectory(cachedFromLASiteFile.DirectoryName!);
                File.WriteAllText(cachedFromLASiteFile.FullName, laSiteJson);
            }

            var liveJson = File.ReadAllText(cachedFromLASiteFile.FullName);
            try
            {
                JsonAssertion.Equivalent(generatedJson, liveJson);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
                
                // slight hack for missing timezone
                throw;
            }
        }

        public const string IdRoot = "https://linked.art/example";
    }
}
