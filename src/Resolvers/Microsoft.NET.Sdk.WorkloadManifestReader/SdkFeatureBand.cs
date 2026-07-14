// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public struct SdkFeatureBand : IEquatable<SdkFeatureBand>, IComparable<SdkFeatureBand>
    {
        private ReleaseVersion _featureBand;

        public SdkFeatureBand(string? version) : this(new ReleaseVersion(version) ?? throw new ArgumentNullException(nameof(version))) { }

        public SdkFeatureBand(ReleaseVersion version)
        {
            var fullVersion = version ?? throw new ArgumentNullException(nameof(version));
            if (string.IsNullOrEmpty(version.Prerelease) || version.Prerelease.Contains("dev") || version.Prerelease.Contains("ci") || version.Prerelease.Contains("rtm"))
            {
                _featureBand = new ReleaseVersion(fullVersion.Major, fullVersion.Minor, fullVersion.SdkFeatureBand);
            }
            else
            {
                // Treat preview versions as their own feature bands
                var prereleaseComponents = fullVersion.Prerelease.Split('.');
                var formattedPrerelease = prereleaseComponents.Length > 1 ?
                    $"{prereleaseComponents[0]}.{prereleaseComponents[1]}"
                    : prereleaseComponents[0];
                _featureBand = new ReleaseVersion(fullVersion.Major, fullVersion.Minor, fullVersion.SdkFeatureBand, formattedPrerelease);
            }
        }

        public static SdkFeatureBand FromWorkloadSetVersion(string workloadSetVersion)
            => FromWorkloadSetVersion(workloadSetVersion, out _);

        public static SdkFeatureBand FromWorkloadSetVersion(string workloadSetVersion, out string packageVersion)
        {
            string[] coreComponents = WorkloadSetVersion.SeparateCoreComponents(workloadSetVersion, out string[] sections);
            string major = coreComponents[0];
            string minor = coreComponents[1];
            string patch = coreComponents[2];
            packageVersion = $"{major}.{patch}.";

            SdkFeatureBand sdkFeatureBand;
            if (coreComponents.Length == 3)
            {
                //  No workload set patch version
                packageVersion += "0";
                //  Use preview specifier (if any) from workload set version as part of SDK feature band
                sdkFeatureBand = new SdkFeatureBand(workloadSetVersion);
            }
            else
            {
                //  Workload set version has workload patch version (ie 4 components)
                packageVersion += coreComponents[3];
                //  Don't include any preview specifiers in SDK feature band
                sdkFeatureBand = new SdkFeatureBand($"{major}.{minor}.{patch}");
            }

            if (sections.Length > 1)
            {
                //  Figure out if we split on a '-' or '+'
                char separator = workloadSetVersion[sections[0].Length];
                packageVersion += separator + sections[1];
            }

            return sdkFeatureBand;
        }

        public bool IsPrerelease => !string.IsNullOrEmpty(_featureBand.Prerelease);
        public int Major => _featureBand.Major;
        public int Minor => _featureBand.Minor;

        public bool Equals(SdkFeatureBand other)
        {
            return _featureBand.Equals(other._featureBand);
        }

        public int CompareTo(SdkFeatureBand other)
        {
            return _featureBand.CompareTo(other._featureBand);
        }

        public override bool Equals(object? obj)
        {
            return obj is SdkFeatureBand featureBand && Equals(featureBand);
        }

        public override int GetHashCode()
        {
            return _featureBand.GetHashCode();
        }

        public override string ToString()
        {
            return _featureBand.ToString();
        }

        public string ToStringWithoutPrerelease()
        {
            return new ReleaseVersion(_featureBand.Major, _featureBand.Minor, _featureBand.SdkFeatureBand).ToString();
        }

        public static bool operator >(SdkFeatureBand a, SdkFeatureBand b) => a.CompareTo(b) > 0;

        public static bool operator <(SdkFeatureBand a, SdkFeatureBand b) => a.CompareTo(b) < 0;

        public string GetWorkloadSetPackageVersion(string packageVersion)
        {
            var releaseVersion = new ReleaseVersion(packageVersion);
            var patch = releaseVersion.Patch > 0 ? $".{releaseVersion.Patch}" : string.Empty;
            var release = string.IsNullOrWhiteSpace(releaseVersion.Prerelease) ? string.Empty : $"-{releaseVersion.Prerelease}";
            return $"{Major}.{Minor}.{releaseVersion.Minor}{patch}{release}";
        }
    }
}
