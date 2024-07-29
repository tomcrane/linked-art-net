using LinkedArtNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PmcTransformer
{
    public static class Writer
    {
        private const string BasePath = @"C:\pmc\full_output";

        private static readonly JsonSerializerOptions options = new() { WriteIndented = true, };

        // make sure this serialises the full object not just laObj fields
        public static void WriteToDisk(LinkedArtObject laObj)
        {
            var json = JsonSerializer.Serialize(laObj, options);
            var path = laObj.GetFilePath(BasePath);
            File.WriteAllText(path, json);
        }


}
}
