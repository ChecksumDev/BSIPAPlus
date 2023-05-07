using Newtonsoft.Json;
using System;

namespace IPA.JsonConverters
{
    internal class MultilineStringConverter : JsonConverter<string>
    {
        public override string ReadJson(JsonReader reader, Type objectType, string existingValue, bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartArray)
            {
                string[] list = serializer.Deserialize<string[]>(reader);
                return string.Join("\n", list);
            }

            return reader.Value as string;
        }

        public override void WriteJson(JsonWriter writer, string value, JsonSerializer serializer)
        {
            string[] list = value.Split('\n');
            if (list.Length == 1)
            {
                serializer.Serialize(writer, value);
            }
            else
            {
                serializer.Serialize(writer, list);
            }
        }
    }
}