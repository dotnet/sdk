// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Microsoft.DotNet.UnifiedBuild.Tasks
{
    public class VersionEntry
    {
        public string Name;
        public NuGetVersion Version;
    }

    /// <summary>
    /// Creates a props file that is used as the input for a repo-level build. The props file
    /// includes package version numbers that should be used by the repo build and additional special properties.
    /// 
    /// There are two types of input props that can be written:
    /// - Versions of union of all packages produced by the builds are added. (AllPackages)
    /// - Only versions of packages that are listed as dependencies of a repo are added. (DependenciesOnly)
    ///
    /// The former represents the current way that source build works for most repos. The latter represents the desired
    /// methodology (PVP Flow). PVP flow closely matches how the product is built in non-source-build mode.
    /// </summary>
    public class WritePackageVersionsProps : Microsoft.Build.Utilities.Task
    {
        private static readonly Regex InvalidElementNameCharRegex = new Regex(@"(^|[^A-Za-z0-9])(?<FirstPartChar>.)");

        public const string CreationTimePropertyName = "BuildOutputPropsCreationTime";
        public const string VersionPropertySuffix = "Version";
        private const string VersionPropertyAlternateSuffix = "PackageVersion";
        private const string PinnedAttributeName = "Pinned";
        private const string DependencyAttributeName = "Dependency";
        private const string NameAttributeName = "Name";

        private const string AllPackagesVersionPropsFlowType = "AllPackages";
        private const string DependenciesOnlyVersionPropsFlowType = "DependenciesOnly";
        private const string DefaultVersionPropsFlowType = AllPackagesVersionPropsFlowType;

        /// <summary>
        /// Set of packages built by dependencies of this repo during this build.
        ///
        /// %(Identity): Package identity.
        /// %(Version): Package version.
        /// </summary>
        [Required]
        public ITaskItem[] KnownPackages { get; set; }

        /// <summary>
        /// File where the version properties should be written.
        /// </summary>
        [Required]
        public string OutputPath { get; set; }

        /// <summary>
        /// Properties to add to the build output props, which may not exist as nupkgs.
        /// For example, this is used to pass the version of the CLI toolset archives.
        /// 
        /// %(Identity): Package identity.
        /// %(Version): Package version.
        /// </summary>
        public ITaskItem[] ExtraProperties { get; set; }

        /// <summary>
        /// Indicates which properties will be written into the Version props file.
        /// If AllPackages (Default), all packages from previously built repos will be written.
        /// If DependenciesOnly, then only those packages appearing as dependencies in
        /// Version.Details.xml will show up. The VersionsDetails property must be set to a 
        /// valid Version.Details.xml path when DependenciesOnly is used.
        /// </summary>
        public string VersionPropsFlowType { get; set; } = DefaultVersionPropsFlowType;

        /// <summary>
        /// If VersionPropsFlowType is set to DependenciesOnly, should be the path to the Version.Detail.xml file for the repo.
        /// </summary>
        public string VersionDetails { get; set; }

        /// <summary>
        /// Retrieve the set of the dependencies from the repo's Version.Details.Xml file.
        /// </summary>
        /// <returns>Hash set of dependency names. </returns>
        private HashSet<string> GetDependences()
        {
            XmlDocument document = new XmlDocument();

            try
            {
                document.Load(VersionDetails);
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e);
                return null;
            }

            HashSet<string> dependencyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Load the nodes, filter those that are not pinned, and 
            XmlNodeList dependencyNodes = document.DocumentElement.SelectNodes($"//{DependencyAttributeName}");

            foreach (XmlNode dependency in dependencyNodes)
            {
                if (dependency.NodeType == XmlNodeType.Comment || dependency.NodeType == XmlNodeType.Whitespace)
                {
                    continue;
                }

                bool isPinned = false;
                XmlAttribute pinnedAttribute = dependency.Attributes[PinnedAttributeName];
                if (pinnedAttribute != null && !bool.TryParse(pinnedAttribute.Value, out isPinned))
                {
                    Log.LogError($"The '{PinnedAttributeName}' attribute is set but the value " +
                        $"'{pinnedAttribute.Value}' is not a valid boolean...");
                    return null;
                }

                if (isPinned)
                {
                    continue;
                }

                var name = dependency.Attributes[NameAttributeName]?.Value?.Trim();

                if (string.IsNullOrEmpty(name))
                {
                    Log.LogError($"The '{NameAttributeName}' attribute must be specified.");
                    return null;
                }

                dependencyNames.Add(name);
            }

            return dependencyNames;
        }

        /// <summary>
        /// Filter a set of input dependencies to those that appear in <paramref name="dependencies"/>
        /// </summary>
        /// <param name="input">Input set of entries</param>
        /// <param name="dependencies">Set of dependencies</param>
        /// <returns>Set of <paramref name="input"/> that appears in <paramref name="dependencies"/></returns>
        private IEnumerable<VersionEntry> FilterNonDependencies(IEnumerable<VersionEntry> input, HashSet<string> dependencies)
        {
            return input.Where(entry => dependencies.Contains(entry.Name));
        }

        public override bool Execute()
        {
            if (VersionPropsFlowType != AllPackagesVersionPropsFlowType &&
                VersionPropsFlowType != DependenciesOnlyVersionPropsFlowType)
            {
                Log.LogError($"Valid version flow types are '{DependenciesOnlyVersionPropsFlowType}' and '{AllPackagesVersionPropsFlowType}'");
                return !Log.HasLoggedErrors;
            }

            if (VersionPropsFlowType == DependenciesOnlyVersionPropsFlowType && (string.IsNullOrEmpty(VersionDetails) || !File.Exists(VersionDetails)))
            {
                Log.LogError($"When version flow type is DependenciesOnly, the VersionDetails task parameter must point to a valid path to the Version.Details.xml file for the repo. " +
                    $"Provided file path '{VersionDetails}' does not exist.");
                return !Log.HasLoggedErrors;
            }

            KnownPackages ??= Array.Empty<ITaskItem>();

            // First, obtain version information from the packages and additional assets that
            // are provided.
            var knownPackages = KnownPackages
                .Select(item => new VersionEntry()
                    {
                        Name = item.GetMetadata("Identity"),
                        Version = new NuGetVersion(item.GetMetadata("Version"))
                    });

            // We may have multiple versions of the same package. We'll keep the latest one.
            // This can even happen in the KnownPackages list, as a repo (such as source-build-reference-packages)
            // may have multiple versions of the same package.
            IEnumerable<VersionEntry> packageElementsToWrite = knownPackages
                .GroupBy(identity => identity.Name)
                .Select(g => g.OrderByDescending(id => id.Version).First())
                .OrderBy(id => id.Name);

            // Then, if version flow type is "DependenciesOnly", filter those
            // dependencies that do not appear in the version.details.xml file.
            if (VersionPropsFlowType == DependenciesOnlyVersionPropsFlowType)
            {
                var dependencies = GetDependences();

                if (Log.HasLoggedErrors)
                {
                    return false;
                }

                packageElementsToWrite = FilterNonDependencies(packageElementsToWrite, dependencies);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

            using (var outStream = File.Open(OutputPath, FileMode.Create))
            using (var sw = new StreamWriter(outStream, new UTF8Encoding(false)))
            {
                sw.WriteLine(@"<Project ToolsVersion=""14.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">");

                WriteVersionEntries(sw, packageElementsToWrite, "packages");
                WriteExtraProperties(sw);

                sw.WriteLine(@"  <PropertyGroup>");
                sw.WriteLine($@"    <{CreationTimePropertyName}>{DateTime.UtcNow.Ticks}</{CreationTimePropertyName}>");
                sw.WriteLine(@"  </PropertyGroup>");

                sw.WriteLine(@"</Project>");
            }

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Write properties specified in the "ExtraProperties task parameter
        /// </summary>
        /// <param name="sw">Stream writer</param>
        private void WriteExtraProperties(StreamWriter sw)
        {
            if (ExtraProperties == null)
            {
                return;
            }

            sw.WriteLine(@"  <!-- Extra properties -->");
            sw.WriteLine(@"  <PropertyGroup>");

            foreach (var extraProp in ExtraProperties ?? Enumerable.Empty<ITaskItem>())
            {
                string propertyName = extraProp.GetMetadata("Identity");
                bool doNotOverwrite = false;
                string overwriteCondition = string.Empty;
                if (bool.TryParse(extraProp.GetMetadata("DoNotOverwrite"), out doNotOverwrite) && doNotOverwrite)
                {
                    overwriteCondition = $" Condition=\"'$({propertyName})' == ''\"";
                }
                sw.WriteLine($"    <{propertyName}{overwriteCondition}>{extraProp.GetMetadata("Version")}</{propertyName}>");
            }

            sw.WriteLine(@"  </PropertyGroup>");
        }

        /// <summary>
        /// Write properties for the version numbers required for this repo.
        /// </summary>
        /// <param name="sw">Stream writer</param>
        /// <param name="entries">Version entries</param>
        private void WriteVersionEntries(StreamWriter sw, IEnumerable<VersionEntry> entries, string entryType)
        {
            if (!entries.Any())
            {
                return;
            }

            sw.WriteLine($"  <!-- Versions of {entryType} produced by earlier stages of the build -->");
            if (VersionPropsFlowType == DependenciesOnlyVersionPropsFlowType)
            {
                sw.WriteLine(@"  <!-- Only those packages/assets that are explicit dependencies of this repo are included. -->");
            }
            sw.WriteLine(@"  <PropertyGroup>");
            foreach (var package in entries)
            {
                string propertyName = GetPropertyName(package.Name, VersionPropertySuffix);
                string alternatePropertyName = GetPropertyName(package.Name, VersionPropertyAlternateSuffix);

                sw.WriteLine($"    <{propertyName}>{package.Version}</{propertyName}>");
                sw.WriteLine($"    <{alternatePropertyName}>{package.Version}</{alternatePropertyName}>");
            }
            sw.WriteLine(@"  </PropertyGroup>");
        }

        public static string GetPropertyName(string id, string suffix)
        {
            string formattedId = InvalidElementNameCharRegex.Replace(
                id,
                match => match.Groups?["FirstPartChar"].Value.ToUpperInvariant()
                    ?? string.Empty);

            return $"{formattedId}{suffix}";
        }
    }
}
