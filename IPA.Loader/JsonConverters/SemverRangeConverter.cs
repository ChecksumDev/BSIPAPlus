#nullable enable
using Hive.Versioning;
using Newtonsoft.Json;
using System;

namespace IPA.JsonConverters
{
    internal class SemverRangeConverter : JsonConverter<VersionRange?>
    {
        public override VersionRange? ReadJson(JsonReader reader, Type objectType, VersionRange? existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            return reader.Value is not string s ? existingValue : new VersionRange(s);
        }

        public override void WriteJson(JsonWriter writer, VersionRange? value, JsonSerializer serializer)
        {
            if (value is null)
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