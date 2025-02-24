// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Workloads.Workload
{
    static class WorkloadSetVersion
    {
        private static string[] SeparateCoreComponents(string workloadSetVersion, out string[] sections)
        {
            sections = workloadSetVersion.Split(['-', '+'], 2);
            if (sections.Length < 1)
            {
                return [];
            }

            return sections[0].Split('.');
        }

        public static bool IsWorkloadSetPackageVersion(string workloadSetVersion)
        {
            int coreComponentsLength = SeparateCoreComponents(workloadSetVersion, out _).Length;
            return coreComponentsLength >= 3 && coreComponentsLength <= 4;
        }

        public static string ToWorkloadSetPackageVersion(string workloadSetVersion, out SdkFeatureBand sdkFeatureBand)
        {
            string[] coreComponents = SeparateCoreComponents(workloadSetVersion, out string[] sections);
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

            if (sections.Length > 1)
            {
                //  Figure out if we split on a '-' or '+'
                char separator = workloadSetVersion[sections[0].Length];
                packageVersion += separator + sections[1];
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
