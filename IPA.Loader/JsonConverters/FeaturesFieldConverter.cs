using IPA.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace IPA.JsonConverters
{
    internal class FeaturesFieldConverter : JsonConverter<Dictionary<string, List<JObject>>>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Assert([DoesNotReturnIf(false)] bool condition)
        {
            if (!condition)
            {
                throw new InvalidOperationException();
            }
        }

        public override Dictionary<string, List<JObject>> ReadJson(JsonReader reader, Type objectType,
            Dictionary<string, List<JObject>> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartArray)
            {
                _ = serializer.Deserialize<string[]>(reader);
                Logger.Features.Warn(
                    "Encountered old features used. They no longer do anything, please move to the new format.");
                return existingValue;
            }

            Dictionary<string, List<JObject>> dict = new();
            Assert(reader.TokenType == JsonToken.StartObject && reader.Read());

            while (reader.TokenType == JsonToken.PropertyName)
            {
                string name = (string)reader.Value;
                Assert(reader.Read());

                List<JObject> list = reader.TokenType == JsonToken.StartObject
                    ? new List<JObject> { serializer.Deserialize<JObject>(reader) }
                    : serializer.Deserialize<List<JObject>>(reader);

                dict.Add(name, list);
                Assert(reader.Read());
            }

            return dict;
        }

        public override void WriteJson(JsonWriter writer, Dictionary<string, List<JObject>> value,
            JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}