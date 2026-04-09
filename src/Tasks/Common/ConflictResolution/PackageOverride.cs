// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

#if EXTENSIONS
using OverrideVersion = System.Version;
#else
using OverrideVersion = NuGet.Versioning.NuGetVersion;
using NuGet.Versioning;
#endif

namespace Microsoft.NET.Build.Tasks.ConflictResolution
{
    /// <summary>
    /// A PackageOverride contains information about a package that overrides
    /// a set of packages up to a certain version.
    /// </summary>
    /// <remarks>
    /// For example, Microsoft.NETCore.App overrides System.Console up to version 4.3.0,
    /// System.IO up to version version 4.3.0, etc.
    /// </remarks>
    internal class PackageOverride
    {
        public string PackageName { get; }
        public Dictionary<string, OverrideVersion> OverriddenPackages { get; }

        private PackageOverride(string packageName, IEnumerable<(string id, OverrideVersion version)> overriddenPackages)
        {
            PackageName = packageName;

            OverriddenPackages = new Dictionary<string, OverrideVersion>(StringComparer.OrdinalIgnoreCase);
            foreach (var package in overriddenPackages)
            {
                OverriddenPackages[package.id] = package.version;
            }
        }

        public static PackageOverride Create(ITaskItem packageOverrideItem)
        {
            string packageName = packageOverrideItem.ItemSpec;
            string overriddenPackagesString = packageOverrideItem.GetMetadata(MetadataKeys.OverriddenPackages);

            return new PackageOverride(packageName, CreateOverriddenPackages(overriddenPackagesString));
        }

        private static IEnumerable<(string id, OverrideVersion version)> CreateOverriddenPackages(string overriddenPackagesString)
        {
            if (!string.IsNullOrEmpty(overriddenPackagesString))
            {
                overriddenPackagesString = overriddenPackagesString.Trim();
                string[] overriddenPackagesAndVersions = overriddenPackagesString.Split(new char[] { ';', '\r', '\n', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                return CreateOverriddenPackages(overriddenPackagesAndVersions);
            }
            return Enumerable.Empty<(string id, OverrideVersion version)>();
        }

        public static IEnumerable<(string id, OverrideVersion version)> CreateOverriddenPackages(IEnumerable<string> packageOverrideFileLines)
        {
            foreach (string overriddenPackagesAndVersion in packageOverrideFileLines)
            {
                string trimmedOverriddenPackagesAndVersion = overriddenPackagesAndVersion.Trim();
                int separatorIndex = trimmedOverriddenPackagesAndVersion.IndexOf('|');
                if (separatorIndex != -1)
                {
                    string versionString = trimmedOverriddenPackagesAndVersion.Substring(separatorIndex + 1);
                    string overriddenPackage = trimmedOverriddenPackagesAndVersion.Substring(0, separatorIndex);
                    if (OverrideVersion.TryParse(versionString, out OverrideVersion? version))
                    {
                        yield return (overriddenPackage, version);
                    }
                }
            }

        }
    }
}
