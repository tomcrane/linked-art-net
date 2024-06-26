﻿using System.Text.Json;
using System.Text.Json.Serialization;

namespace LinkedArtNet
{
    internal class SingleStringJsonLDConverter : JsonConverter<string[]?>
    {
        public override string[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, string[]? value, JsonSerializerOptions options)
        {
            if(value != null && value.Length > 0)
            {
                if (value.Length == 1)
                {
                    writer.WriteStringValue(value[0]);
                } 
                else
                {
                    writer.WriteStartArray();
                    foreach (var s in value)
                    {
                        writer.WriteStringValue(s);
                    }
                    writer.WriteEndArray();
                }
            }
        }
    }
}