using Hive.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using HVersion = Hive.Versioning.Version;

namespace SemVer
{
    [Obsolete("Use Hive.Versioning.VersionRange instead.")]
    public class Range : IEquatable<Range>, IEquatable<VersionRange>
    {
        private Range(VersionRange real)
        {
            UnderlyingRange = real;
        }

        public Range(string rangeSpec, bool loose = false) : this(new VersionRange(rangeSpec))
        {
            _ = loose;
            // loose is ignored because Hive doesn't have an equivalent
        }

        public VersionRange UnderlyingRange { get; }

        public bool Equals(Range? other)
        {
            return UnderlyingRange.Equals(other?.UnderlyingRange);
        }

        public bool Equals(VersionRange? other)
        {
            return UnderlyingRange.Equals(other);
        }

        public static Range ForHiveRange(VersionRange real)
        {
            return new Range(real);
        }

        public bool IsSatisfied(Version version)
        {
            return IsSatisfied(version.UnderlyingVersion);
        }

        public bool IsSatisfied(HVersion version)
        {
            return UnderlyingRange.Matches(version);
        }

        public bool IsSatisfied(string versionString, bool loose = false)
        {
            return IsSatisfied(new Version(versionString, loose));
        }

        public IEnumerable<Version> Satisfying(IEnumerable<Version> versions)
        {
            return versions.Where(IsSatisfied);
        }

        public IEnumerable<string> Satisfying(IEnumerable<string> versions, bool loose = false)
        {
            return versions.Where(v => IsSatisfied(v, loose));
        }

        public Version? MaxSatisfying(IEnumerable<Version> versions)
        {
            return Satisfying(versions).Max();
        }

        public string? MaxSatisfying(IEnumerable<string> versionStrings, bool loose = false)
        {
            return MaxSatisfying(ValidVersions(versionStrings, loose))?.ToString();
        }

        public Range Intersect(Range other)
        {
            return new Range(UnderlyingRange & other.UnderlyingRange);
            // the conjunction is the intersection
        }

        public override string ToString()
        {
            return UnderlyingRange.ToString();
        }

        public override bool Equals(object? obj)
        {
            return obj switch
            {
                Range r => Equals(r),
                VersionRange vr => Equals(vr),
                _ => false
            };
        }

        public static bool operator ==(Range? a, Range? b)
        {
            return a?.Equals(b) ?? b is null;
        }

        public static bool operator !=(Range? a, Range? b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return UnderlyingRange.GetHashCode();
        }

        public static bool IsSatisfied(string rangeSpec, string versionString, bool loose = false)
        {
            return new Range(rangeSpec, loose).IsSatisfied(versionString, loose);
        }

        public static IEnumerable<string> Satisfying(string rangeSpec, IEnumerable<string> versions, bool loose = false)
        {
            return new Range(rangeSpec, loose).Satisfying(versions, loose);
        }

        public static string? MaxSatisfying(string rangeSpec, IEnumerable<string> versions, bool loose = false)
        {
            return new Range(rangeSpec, loose).MaxSatisfying(versions, loose);
        }

        private IEnumerable<Version> ValidVersions(IEnumerable<string> versionStrings, bool loose)
        {
            foreach (string versionString in versionStrings)
            {
                Version? version = null;
                try
                {
                    version = new Version(versionString, loose);
                }
                catch (ArgumentException)
                {
                }

                if (version is not null)
                {
                    yield return version;
                }
            }
        }
    }
}