using IPA.Utilities;
using Newtonsoft.Json;
using System;

namespace IPA.JsonConverters
{
    internal class AlmostVersionConverter : JsonConverter<AlmostVersion>
    {
        public override AlmostVersion ReadJson(JsonReader reader, Type objectType, AlmostVersion existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            return reader.Value == null ? null : new AlmostVersion(reader.Value as string);
        }

        public override void WriteJson(JsonWriter writer, AlmostVersion value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                writer.WriteValue(value.ToString());
            }
        }
    }
}