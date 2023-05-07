#nullable enable
using Hive.Versioning;
using IPA.JsonConverters;
using IPA.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using AlmostVersionConverter = IPA.JsonConverters.AlmostVersionConverter;
using Version = Hive.Versioning.Version;
#if NET3
using Net3_Proxy;
using Array = Net3_Proxy.Array;
#endif

namespace IPA.Loader
{
    internal class PluginManifest
    {
        [JsonProperty("author", Required = Required.Always)]
        public string Author = null!;

        [JsonProperty("conflictsWith", Required = Required.DisallowNull,
            ItemConverterType = typeof(SemverRangeConverter))]
        public Dictionary<string, VersionRange> Conflicts = new();

        [JsonProperty("dependsOn", Required = Required.DisallowNull, ItemConverterType = typeof(SemverRangeConverter))]
        public Dictionary<string, VersionRange> Dependencies = new();

        [JsonProperty("description", Required = Required.Always)] [JsonConverter(typeof(MultilineStringConverter))]
        public string Description = null!;

        [JsonProperty("features", Required = Required.DisallowNull)] [JsonConverter(typeof(FeaturesFieldConverter))]
        public Dictionary<string, List<JObject>> Features = new();

        [JsonProperty("files", Required = Required.DisallowNull)]
        public string[] Files = Array.Empty<string>();

        [JsonProperty("gameVersion", Required = Required.DisallowNull)] [JsonConverter(typeof(AlmostVersionConverter))]
        public AlmostVersion? GameVersion;

        [JsonProperty("icon", Required = Required.DisallowNull)]
        public string? IconPath;

        [JsonProperty("id", Required = Required.AllowNull)] // TODO: on major version bump, make this always
        public string? Id;

        [JsonProperty("links", Required = Required.DisallowNull)]
        public LinksObject? Links;

        [JsonProperty("loadAfter", Required = Required.DisallowNull)]
        public string[] LoadAfter = Array.Empty<string>();

        [JsonProperty("loadBefore", Required = Required.DisallowNull)]
        public string[] LoadBefore = Array.Empty<string>();

        [JsonProperty("misc", Required = Required.DisallowNull)]
        public MiscObject? Misc;

        [JsonProperty("name", Required = Required.Always)]
        public string Name = null!;

        [JsonProperty("version", Required = Required.Always)] [JsonConverter(typeof(SemverVersionConverter))]
        public Version Version = null!;

        [Serializable]
        public class LinksObject
        {
            [JsonProperty("donate", Required = Required.DisallowNull)]
            public Uri? Donate;

            [JsonProperty("project-home", Required = Required.DisallowNull)]
            public Uri? ProjectHome;

            [JsonProperty("project-source", Required = Required.DisallowNull)]
            public Uri? ProjectSource;
        }

        [Serializable]
        public class MiscObject
        {
            [JsonProperty("plugin-hint", Required = Required.DisallowNull)]
            public string? PluginMainHint;
        }
    }
}