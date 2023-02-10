// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace Microsoft.DotNet.SourceBuild.Tasks
{
    /// <summary>
    /// Writes a props file to the given directory for each dependency specified
    /// plus adds or updates an existing props file with all dependencies.  The 
    /// intention is for the props file to be included by a source-build build
    /// to get metadata about each dependent repo.
    /// </summary>
    public class Tarball_WriteSourceRepoProperties : Task
    {
        /// <summary>
        /// The directory to write the props files to.
        /// </summary>
        [Required]
        public string SourceBuildMetadataDir { get; set; }

        /// <summary>
        /// Dependencies to include in the props files.
        /// 
        /// %(Identity): NuGet package ID.
        /// %(Name): The Name of the dependency from Version.Details.xml.
        /// %(ExactVersion): NuGet package version. This can be used to look up the restored package
        ///   contents in a package cache.
        /// %(Version): NuGet package version, wrapped in "[version]" syntax for exact match.
        /// %(Uri): The URI for the repo.
        /// %(Sha): The commit Sha for the dependency.
        /// %(SourceBuildRepoName): The repo name to use in source-build.
        /// </summary>
        /// <value></value>
        [Required]
        public ITaskItem[] Dependencies { get; set; }

        public override bool Execute()
        {
            var allRepoProps = new Dictionary<string, string>();

            foreach (var dependency in Dependencies.Select(dep => 
                new {
                    Name = dep.GetMetadata("Name"),
                    SourceBuildRepoName = dep.GetMetadata("SourceBuildRepoName"),
                    Version = dep.GetMetadata("ExactVersion"),
                    Sha = dep.GetMetadata("Sha"),
                    Uri = dep.GetMetadata("Uri"),
                    GitCommitCount = dep.GetMetadata("GitCommitCount")
                }))
            {
                string repoName = dependency.SourceBuildRepoName;
                string safeRepoName = repoName.Replace("-", "").Replace(".", "");
                string propsPath = Path.Combine(SourceBuildMetadataDir, $"{repoName.Replace(".", "-")}.props");
                DerivedVersion derivedVersion = GetVersionInfo(safeRepoName, dependency.Version, "0");
                var repoProps = new Dictionary<string, string>
                {
                    ["GitCommitHash"] = dependency.Sha,
                    ["OfficialBuildId"] = derivedVersion.OfficialBuildId,
                    ["OutputPackageVersion"] = dependency.Version,
                    ["PreReleaseVersionLabel"] = derivedVersion.PreReleaseVersionLabel,
                };
                if (!string.IsNullOrEmpty(dependency.GitCommitCount))
                {
                    repoProps.Add("GitCommitCount", dependency.GitCommitCount);
                }                
                WritePropsFile(propsPath, repoProps);
                allRepoProps[$"{safeRepoName}GitCommitHash"] = dependency.Sha;
                allRepoProps[$"{safeRepoName}OutputPackageVersion"] = dependency.Version;
            }
            string allRepoPropsPath = Path.Combine(SourceBuildMetadataDir, "AllRepoVersions.props");
            Log.LogMessage(MessageImportance.Normal, $"[{DateTimeOffset.Now}] Writing all repo versions to {allRepoPropsPath}");
            UpdatePropsFile(allRepoPropsPath, allRepoProps);

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Reverse a version in the Arcade style (https://github.com/dotnet/arcade/blob/fb92b14d8cd07cf44f8f7eefa8ac58d7ffd05f3f/src/Microsoft.DotNet.Arcade.Sdk/tools/Version.BeforeCommonTargets.targets#L18)
        /// back to an OfficialBuildId + ReleaseLabel which we can then supply to get the same resulting version number.
        /// </summary>
        /// <param name="repoName">The source build name of the repo to get the version info for.</param>
        /// <param name="version">The complete version, e.g. 1.0.0-beta1-19720.5</param>
        /// <param name="commitCount">The current commit count of the repo.  This is used for some repos that do not use the standard versioning scheme.</param>
        /// <returns></returns>
        private static DerivedVersion GetVersionInfo(string repoName, string version, string commitCount)
        {
            var nugetVersion = new NuGetVersion(version);

            if (!string.IsNullOrWhiteSpace(nugetVersion.Release))
            {
                var releaseParts = nugetVersion.Release.Split('-', '.');
                if (repoName.Contains("nuget"))
                {
                    // NuGet does this - arbitrary build IDs
                    return new DerivedVersion { OfficialBuildId = DateTime.Now.ToString("yyyyMMdd.1"), PreReleaseVersionLabel = releaseParts[0] };
                }
                else if (releaseParts.Length == 3)
                {
                    // VSTest uses full dates for the first part of their preview build numbers
                    if (repoName.Contains("vstest"))
                    {
                        return new DerivedVersion { OfficialBuildId = $"{releaseParts[1]}.{releaseParts[2]}", PreReleaseVersionLabel = releaseParts[0] };
                    }
                    else if (int.TryParse(releaseParts[1], out int datePart) && int.TryParse(releaseParts[2], out int buildPart))
                    {
                        if (datePart > 1 && datePart < 8 && buildPart > 1000 && buildPart < 10000)
                        {
                            return new DerivedVersion { OfficialBuildId = releaseParts[2], PreReleaseVersionLabel = $"{releaseParts[0]}.{releaseParts[1]}" };
                        }
                        else
                        {
                            return new DerivedVersion { OfficialBuildId = $"20{((datePart / 1000))}{((datePart % 1000) / 50):D2}{(datePart % 50):D2}.{buildPart}", PreReleaseVersionLabel = releaseParts[0] };
                        }
                    }
                }
                else if (releaseParts.Length == 4)
                {
                    // new preview version style, e.g. 5.0.0-preview.7.20365.12
                    if (int.TryParse(releaseParts[2], out int datePart) && int.TryParse(releaseParts[3], out int buildPart))
                    {
                        return new DerivedVersion { OfficialBuildId = $"20{((datePart / 1000))}{((datePart % 1000) / 50):D2}{(datePart % 50):D2}.{buildPart}", PreReleaseVersionLabel = $"{releaseParts[0]}.{releaseParts[1]}" };
                    }
                }
            }
            else
            {
                // finalized version number (x.y.z) - probably not our code
                // Application Insights, Newtonsoft.Json do this
                return new DerivedVersion { OfficialBuildId = DateTime.Now.ToString("yyyyMMdd.1"), PreReleaseVersionLabel = string.Empty };
            }

            throw new FormatException($"Can't derive a build ID from version {version} (commit count {commitCount}, release {string.Join(";", nugetVersion.Release.Split('-', '.'))})");
        }

        private static void UpdatePropsFile(string filePath, Dictionary<string, string> properties)
        {
            if (!File.Exists(filePath))
            {
                WritePropsFile(filePath, properties);
            }
            else
            {
                var content = new StringBuilder();
                foreach (var line in File.ReadAllLines(filePath))
                {
                    content.AppendLine(line);
                    if (line.Contains("<PropertyGroup>"))
                    {
                        foreach (var propName in properties.Keys.OrderBy(k => k))
                        {
                            content.AppendLine($"    <{propName}>{properties[propName]}</{propName}>");
                        }
                    }
                }
                File.WriteAllText(filePath, content.ToString());
            }
        }

        private static void WritePropsFile(string filePath, Dictionary<string, string> properties)
        {
            var content = new StringBuilder();
            content.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            content.AppendLine("<Project>");
            content.AppendLine("  <PropertyGroup>");
            foreach (var propName in properties.Keys.OrderBy(k => k))
            {
                content.AppendLine($"    <{propName}>{properties[propName]}</{propName}>");
            }
            content.AppendLine("  </PropertyGroup>");
            content.AppendLine("</Project>");
            File.WriteAllText(filePath, content.ToString());
        }

        private class DerivedVersion
        {
            internal string OfficialBuildId { get; set; }
            internal string PreReleaseVersionLabel { get; set; }
        }
    }
}
