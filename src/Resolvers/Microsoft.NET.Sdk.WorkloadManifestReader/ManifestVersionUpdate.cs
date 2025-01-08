// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    //  TODO: Do we need this class, or the existing version information anymore now that workload manifest are side by side?
    public class ManifestVersionUpdate : IEquatable<ManifestVersionUpdate>, IComparable<ManifestVersionUpdate>
    {
        public ManifestVersionUpdate(ManifestId manifestId, ManifestVersion? newVersion, string? newFeatureBand)
        {
            ManifestId = manifestId;
            NewVersion = newVersion;
            NewFeatureBand = newFeatureBand;
        }

        public ManifestId ManifestId { get; }
        public ManifestVersion? NewVersion { get; }
        public string? NewFeatureBand { get; }

        public int CompareTo(ManifestVersionUpdate? other)
        {
            if (other == null) return 1;
            int ret = ManifestId.CompareTo(other.ManifestId);
            if (ret != 0) return ret;

            if (NewVersion == null && other.NewVersion != null) return -1;
            if (NewVersion != null && other.NewVersion == null) return 1;
            if (NewVersion != null)
            {
                ret = NewVersion.CompareTo(other.NewVersion);
                if (ret != 0) return ret;
            }

            ret = string.Compare(NewFeatureBand, other.NewFeatureBand, StringComparison.Ordinal);
            return ret;
        }
        public bool Equals(ManifestVersionUpdate? other)
        {
            if (other == null) return false;
            return EqualityComparer<ManifestId>.Default.Equals(ManifestId, other.ManifestId) &&
                EqualityComparer<ManifestVersion?>.Default.Equals(NewVersion, other.NewVersion) &&
                string.Equals(NewFeatureBand, other.NewFeatureBand, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is ManifestVersionUpdate id && Equals(id);
        }

        public override int GetHashCode()
        {
#if NETCOREAPP3_1_OR_GREATER
            return HashCode.Combine(ManifestId, NewVersion, NewFeatureBand);
#else
            int hashCode = 1601069575;
            hashCode = hashCode * -1521134295 + ManifestId.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<ManifestVersion?>.Default.GetHashCode(NewVersion);
            hashCode = hashCode * -1521134295 + EqualityComparer<string?>.Default.GetHashCode(NewFeatureBand);
            return hashCode;
#endif
        }
    }
}
