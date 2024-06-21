using LinkedArtNet;
using Microsoft.Recognizers.Text.Matcher;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PmcTransformer.Helpers
{
    public static class Language
    {
        const string GettyAatLanguagesSource = "https://raw.githubusercontent.com/digirati-co-uk/pmc-lux/main/mappings/common/lang-aat.json";
        private static Dictionary<string, string>? GettyAatLanguages = null;

        const string ThreeToTwoLetterCodesSource = "https://raw.githubusercontent.com/digirati-co-uk/pmc-lux/main/mappings/common/lang3-2.json";
        private static Dictionary<string, string>? ThreeToTwoLetterCodes = null;

        static Language()
        {
            var httpClient = new HttpClient();

            var resp1 = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, GettyAatLanguagesSource));
            var stream1 = resp1.Content.ReadAsStream();
            GettyAatLanguages = JsonSerializer.Deserialize<Dictionary<string, string>>(stream1);

            var resp2 = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, ThreeToTwoLetterCodesSource));
            var stream2 = resp2.Content.ReadAsStream();
            ThreeToTwoLetterCodes = JsonSerializer.Deserialize<Dictionary<string, string>>(stream2);
        }

        public static LinkedArtObject? GetLanguage(string code, string? label = null)
        {
            code = code.Trim().ToLowerInvariant();
            if(code.HasText())
            {
                var aatLookupCode = code;
                if(ThreeToTwoLetterCodes!.ContainsKey(code))
                {
                    aatLookupCode = ThreeToTwoLetterCodes[code];
                }
                if (aatLookupCode.HasText())
                {
                    if(GettyAatLanguages!.ContainsKey(aatLookupCode))
                    {
                        if (string.IsNullOrWhiteSpace(label))
                        {
                            label = aatLookupCode;
                        }
                        return new LinkedArtObject(Types.Language) 
                        { 
                            Id = GettyAatLanguages[aatLookupCode],
                            Label = label 
                        };
                    }
                }

            }

            return null;
        }
    }
}
