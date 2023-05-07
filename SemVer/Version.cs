using System;
using System.Linq;
using HVersion = Hive.Versioning.Version;

namespace SemVer
{
    [Obsolete("Use Hive.Versioning.Version instead.")]
    public class Version : IComparable<Version>, IComparable<HVersion>, IComparable, IEquatable<Version>,
        IEquatable<HVersion>
    {
        private Version(HVersion real)
        {
            UnderlyingVersion = real;
        }

        public Version(string input, bool loose = false) : this(new HVersion(input))
        {
            _ = loose;
            // specifically unused because Hive has no equivalent (by design)
        }

        public Version(int major, int minor, int patch, string? preRelease = null, string? build = null)
            : this(new HVersion(major, minor, patch,
                preRelease is null ? Enumerable.Empty<string>() : preRelease.Split('.'),
                build is null ? Enumerable.Empty<string>() : build.Split('.')))
        {
        }

        public HVersion UnderlyingVersion { get; }

        public int Major => (int)UnderlyingVersion.Major;
        public int Minor => (int)UnderlyingVersion.Minor;
        public int Patch => (int)UnderlyingVersion.Patch;
        public string PreRelease => string.Join(".", UnderlyingVersion.PreReleaseIds);
        public string Build => string.Join(".", UnderlyingVersion.BuildIds);

        public int CompareTo(object? obj)
        {
            return obj switch
            {
                null => 1,
                Version v => CompareTo(v),
                HVersion h => CompareTo(h),
                _ => throw new ArgumentException("Object is not a Version")
            };
        }

        public int CompareTo(Version? other)
        {
            return UnderlyingVersion.CompareTo(other?.UnderlyingVersion);
        }

        public int CompareTo(HVersion? other)
        {
            return UnderlyingVersion.CompareTo(other);
        }

        public bool Equals(Version? other)
        {
            return UnderlyingVersion.Equals(other?.UnderlyingVersion);
        }

        public bool Equals(HVersion? other)
        {
            return UnderlyingVersion.Equals(other);
        }

        public static Version ForHiveVersion(HVersion real)
        {
            return new Version(real);
        }

        public Version BaseVersion()
        {
            return new Version(new HVersion(UnderlyingVersion.Major, UnderlyingVersion.Minor, UnderlyingVersion.Patch));
        }

        public override string ToString()
        {
            return UnderlyingVersion.ToString();
        }

        public string Clean()
        {
            return ToString();
            // normally this is the other way around kek
        }

        public override int GetHashCode()
        {
            return UnderlyingVersion.GetHashCode();
        }

        public override bool Equals(object? obj)
        {
            return obj switch
            {
                Version v => Equals(v),
                HVersion h => Equals(h),
                _ => false
            };
        }

        public static bool operator ==(Version? a, Version? b)
        {
            return a?.UnderlyingVersion == b?.UnderlyingVersion;
        }

        public static bool operator !=(Version? a, Version? b)
        {
            return a?.UnderlyingVersion != b?.UnderlyingVersion;
        }

        public static bool operator >(Version? a, Version? b)
        {
            return a is null ? b is not null && b.CompareTo(a) < 0 : a.CompareTo(b) > 0;
        }

        public static bool operator >=(Version? a, Version? b)
        {
            return !(a < b);
        }

        public static bool operator <(Version? a, Version? b)
        {
            return a is null ? b is not null && b.CompareTo(a) > 0 : a.CompareTo(b) < 0;
        }

        public static bool operator <=(Version? a, Version? b)
        {
            return !(a > b);
        }
    }
}