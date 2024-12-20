// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;
using System.Text.Json;
using FluentAssertions.Extensions;
using ManifestReaderTests;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests
{
    public class TestManifestUpdate
    {
        public TestManifestUpdate(ManifestId manifestId, ManifestVersion existingVersion, string existingFeatureBand, ManifestVersion newVersion, string newFeatureBand)
        {
            ManifestId = manifestId;
            ExistingVersion = existingVersion;
            ExistingFeatureBand = existingFeatureBand;
            NewVersion = newVersion;
            NewFeatureBand = newFeatureBand;
        }

        public ManifestId ManifestId { get; }
        public ManifestVersion ExistingVersion { get; }
        public string ExistingFeatureBand { get; }
        public ManifestVersion NewVersion { get; }
        public string NewFeatureBand { get; }

        //  Returns an object representing an undo of this manifest update
        //public TestManifestUpdate Reverse()
        //{
        //    return new TestManifestUpdate(ManifestId, NewVersion, NewFeatureBand, ExistingVersion, ExistingFeatureBand);
        //}

        public int CompareTo(TestManifestUpdate other)
        {
            if (other == null) return 1;
            int ret = ManifestId.CompareTo(other.ManifestId);
            if (ret != 0) return ret;

            if (ExistingVersion == null && other.ExistingVersion != null) return -1;
            if (ExistingVersion != null && other.ExistingVersion == null) return 1;
            if (ExistingVersion != null)
            {
                ret = ExistingVersion.CompareTo(other.ExistingVersion);
                if (ret != 0) return ret;
            }

            ret = string.Compare(ExistingFeatureBand, other.ExistingFeatureBand, StringComparison.Ordinal);
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
        public bool Equals(TestManifestUpdate other)
        {
            if (other == null) return false;
            return EqualityComparer<ManifestId>.Default.Equals(ManifestId, other.ManifestId) &&
                EqualityComparer<ManifestVersion>.Default.Equals(ExistingVersion, other.ExistingVersion) &&
                string.Equals(ExistingFeatureBand, other.ExistingFeatureBand, StringComparison.Ordinal) &&
                EqualityComparer<ManifestVersion>.Default.Equals(NewVersion, other.NewVersion) &&
                string.Equals(NewFeatureBand, other.NewFeatureBand, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is TestManifestUpdate id && Equals(id);
        }

        public override int GetHashCode()
        {
#if NETCOREAPP3_1_OR_GREATER
            return HashCode.Combine(ManifestId, ExistingVersion, ExistingFeatureBand, NewVersion, NewFeatureBand);
#else
            int hashCode = 1601069575;
            hashCode = hashCode * -1521134295 + ManifestId.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<ManifestVersion?>.Default.GetHashCode(ExistingVersion);
            hashCode = hashCode * -1521134295 + EqualityComparer<string?>.Default.GetHashCode(ExistingFeatureBand);
            hashCode = hashCode * -1521134295 + EqualityComparer<ManifestVersion?>.Default.GetHashCode(NewVersion);
            hashCode = hashCode * -1521134295 + EqualityComparer<string?>.Default.GetHashCode(NewFeatureBand);
            return hashCode;
#endif
        }

        public ManifestVersionUpdate ToManifestVersionUpdate()
        {
            return new(ManifestId, NewVersion, NewFeatureBand);
        }

    }
}
