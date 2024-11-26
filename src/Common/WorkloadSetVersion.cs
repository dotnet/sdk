// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Workloads.Workload
{
    static class WorkloadSetVersion
    {
        public static bool IsWorkloadSetPackageVersion(string workloadSetVersion)
        {

            string[] sections = workloadSetVersion.Split(['-', '+'], 2);
            string versionCore = sections[0];
            string? preReleaseOrBuild = sections.Length > 1 ? sections[1] : null;

            string[] coreComponents = versionCore.Split('.');
            return coreComponents.Length >= 3 && coreComponents.Length <= 4;
        }

        public static string ToWorkloadSetPackageVersion(string workloadSetVersion, out SdkFeatureBand sdkFeatureBand)
        {
            string[] sections = workloadSetVersion.Split(['-', '+'], 2);
            string versionCore = sections[0];
            string? preReleaseOrBuild = sections.Length > 1 ? sections[1] : null;

            string[] coreComponents = versionCore.Split('.');
            string major = coreComponents[0];
            string minor = coreComponents[1];
            string patch = coreComponents[2];

            string packageVersion = $"{major}.{patch}.";
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

            if (preReleaseOrBuild != null)
            {
                //  Figure out if we split on a '-' or '+'
                char separator = workloadSetVersion[sections[0].Length];
                packageVersion += separator + preReleaseOrBuild;
            }

            return packageVersion;
        }

        public static SdkFeatureBand GetFeatureBand(string workloadSetVersion)
        {
            ToWorkloadSetPackageVersion(workloadSetVersion, out SdkFeatureBand sdkFeatureBand);
            return sdkFeatureBand;
        }

        public static string FromWorkloadSetPackageVersion(SdkFeatureBand sdkFeatureBand, string packageVersion)
        {
            var releaseVersion = new ReleaseVersion(packageVersion);
            var patch = releaseVersion.Patch > 0 ? $".{releaseVersion.Patch}" : string.Empty;
            var release = string.IsNullOrWhiteSpace(releaseVersion.Prerelease) ? string.Empty : $"-{releaseVersion.Prerelease}";
            return $"{sdkFeatureBand.Major}.{sdkFeatureBand.Minor}.{releaseVersion.Minor}{patch}{release}";
        }
    }
}
