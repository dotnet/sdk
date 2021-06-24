// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.SourceBuild.Tasks.Models;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace Microsoft.DotNet.Build.Tasks
{
    public class WriteSourceRepoProperties : Task
    {
        [Required]
        public string VersionDetailsFile { get; set; }

        [Required]
        public string ClonedSubmoduleGitRootDirectory { get; set; }

        [Required]
        public string ClonedSubmoduleDirectory { get; set; }

        [Required]
        public string SourceBuildMetadataDir { get; set; }

        public override bool Execute()
        {
            var serializer = new XmlSerializer(typeof(VersionDetails));

            VersionDetails versionDetails = null;
            using (var stream = File.OpenRead(VersionDetailsFile))
            {
                versionDetails = (VersionDetails)serializer.Deserialize(stream);
            }

            var allRepoProps = new Dictionary<string, string>();

            foreach (var dep in versionDetails.ToolsetDependencies.Concat(versionDetails.ProductDependencies))
            {
                Log.LogMessage(MessageImportance.Normal, $"[{DateTimeOffset.Now}] Starting dependency {dep.ToString()}");
                string repoPath = DeriveRepoPath(ClonedSubmoduleDirectory, dep.Uri, dep.Sha);
                string repoGitDir = DeriveRepoGitDirPath(ClonedSubmoduleGitRootDirectory, dep.Uri);
                if (Directory.Exists(repoGitDir))
                {
                    foreach (string repoName in GetRepoNamesOrDefault(dep))
                    {
                        string safeRepoName = repoName.Replace("-", "");
                        try
                        {
                            WriteMinimalMetadata(repoPath, dep.Uri, dep.Sha);
                            WriteSourceBuildMetadata(SourceBuildMetadataDir, repoGitDir, dep);
                            if (File.Exists(Path.Combine(repoPath, ".gitmodules")))
                            {
                                HandleSubmodules(repoPath, repoGitDir, dep);
                            }
                            allRepoProps[$"{safeRepoName}GitCommitHash"] = dep.Sha;
                            allRepoProps[$"{safeRepoName}OutputPackageVersion"] = dep.Version;
                        }
                        catch (Exception e)
                        {
                            Log.LogErrorFromException(e, true, true, null);
                        }
                    }
                }
                else
                {
                    Log.LogMessage(MessageImportance.Normal, $"[{DateTimeOffset.Now}] Skipping dependency {dep.ToString()} - git dir {repoGitDir} doesn't exist");
                }
            }
            string allRepoPropsPath = Path.Combine(SourceBuildMetadataDir, "AllRepoVersions.props");
            Log.LogMessage(MessageImportance.Normal, $"[{DateTimeOffset.Now}] Writing all repo versions to {allRepoPropsPath}");
            WritePropsFile(allRepoPropsPath, allRepoProps);

            return !Log.HasLoggedErrors;
        }

        private void WriteSourceBuildMetadata(string sourceBuildMetadataPath, string repoGitDir, Dependency dependency)
        {
            foreach (string repoName in GetRepoNamesOrDefault(dependency))
            {
                string propsPath = Path.Combine(sourceBuildMetadataPath, $"{repoName}.props");
                string commitCount = GetCommitCount(repoGitDir, dependency.Sha);
                DerivedVersion derivedVersion = GetVersionInfo(dependency.Version, commitCount);
                var repoProps = new Dictionary<string, string>
                {
                    ["GitCommitHash"] = dependency.Sha,
                    ["GitCommitCount"] = commitCount,
                    ["GitCommitDate"] = GetCommitDate(repoGitDir, dependency.Sha),
                    ["OfficialBuildId"] = derivedVersion.OfficialBuildId,
                    ["OutputPackageVersion"] = dependency.Version,
                    ["PreReleaseVersionLabel"] = derivedVersion.PreReleaseVersionLabel,
                    ["IsStable"] = string.IsNullOrWhiteSpace(derivedVersion.PreReleaseVersionLabel) ? "true" : "false",
                };
                WritePropsFile(propsPath, repoProps);
            }
        }


        /// <summary>
        /// Reverse a version in the Arcade style (https://github.com/dotnet/arcade/blob/fb92b14d8cd07cf44f8f7eefa8ac58d7ffd05f3f/src/Microsoft.DotNet.Arcade.Sdk/tools/Version.BeforeCommonTargets.targets#L18)
        /// back to an OfficialBuildId + ReleaseLabel which we can then supply to get the same resulting version number.
        /// </summary>
        /// <param name="version">The complete version, e.g. 1.0.0-beta1-19720.5</param>
        /// <param name="commitCount">The current commit count of the repo.  This is used for some repos that do not use the standard versioning scheme.</param>
        /// <returns></returns>
        private static DerivedVersion GetVersionInfo(string version, string commitCount)
        {
            var nugetVersion = new NuGetVersion(version);

            if (!string.IsNullOrWhiteSpace(nugetVersion.Release))
            {
                var releaseParts = nugetVersion.Release.Split('-', '.');
                if (releaseParts.Length == 2)
                {
                    if (releaseParts[1].TrimStart('0') == commitCount)
                    {
                        // core-sdk does this - OfficialBuildId is only used for their fake package and not in anything shipped
                        return new DerivedVersion { OfficialBuildId = DateTime.Now.ToString("yyyyMMdd.1"), PreReleaseVersionLabel = releaseParts[0] };
                    }
                    else
                    {
                        // NuGet does this - arbitrary build IDs
                        return new DerivedVersion { OfficialBuildId = releaseParts[1], PreReleaseVersionLabel = releaseParts[0] };
                    }
                }
                else if (releaseParts.Length == 3)
                {
                    // VSTest uses full dates for the first part of their preview build numbers
                    if (DateTime.TryParseExact(releaseParts[1], "yyyyMMdd", new CultureInfo("en-US"), DateTimeStyles.AssumeLocal, out DateTime fullDate))
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
                // VSTest, Application Insights, Newtonsoft.Json do this
                return new DerivedVersion { OfficialBuildId = DateTime.Now.ToString("yyyyMMdd.1"), PreReleaseVersionLabel = string.Empty };
            }

            throw new FormatException($"Can't derive a build ID from version {version} (commit count {commitCount}, release {string.Join(";", nugetVersion.Release.Split('-', '.'))})");
        }

        private static string GetDefaultRepoNameFromUrl(string repoUrl)
        {
            if (repoUrl.EndsWith(".git"))
            {
                repoUrl = repoUrl.Substring(0, repoUrl.Length - ".git".Length);
            }
            return repoUrl.Substring(repoUrl.LastIndexOf("/") + 1);
        }

        private static IEnumerable<string> GetRepoNamesOrDefault(Dependency dependency)
        {
            return dependency.RepoNames ?? new[] { GetDefaultRepoNameFromUrl(dependency.Uri) };
        }

        private static string DeriveRepoGitDirPath(string gitDirPath, string repoUrl)
        {
            return Path.Combine(gitDirPath, $"{GetDefaultRepoNameFromUrl(repoUrl)}.git");
        }

        private static string DeriveRepoPath(string sourceDirPath, string repoUrl, string hash)
        {
            // hash could actually be a branch or tag, make it filename-safe
            hash = hash.Replace('/', '-').Replace('\\', '-').Replace('?', '-').Replace('*', '-').Replace(':', '-').Replace('|', '-').Replace('"', '-').Replace('<', '-').Replace('>', '-');
            return Path.Combine(sourceDirPath, $"{GetDefaultRepoNameFromUrl(repoUrl)}.{hash}");
        }

        private string GetCommitCount(string gitDir, string hash)
        {
            return RunGitCommand($"rev-list --count {hash}", gitDir: gitDir);
        }

        private string GetCommitDate(string gitDir, string hash)
        {
            return RunGitCommand($"log -1 --format=%cd --date=short {hash}", gitDir: gitDir);
        }

        private IEnumerable<SubmoduleInfo> GetSubmoduleInfo(string gitModulesFilePath)
        {
            string submoduleProps = RunGitCommand($"config --file={gitModulesFilePath} --list");
            var submodulePathRegex = new Regex(@"submodule\.(?<submoduleName>.*)\.path=(?<submodulePath>.*)");
            foreach (Match m in submodulePathRegex.Matches(submoduleProps))
            {
                yield return new SubmoduleInfo { Name = m.Groups["submoduleName"].Value, Path = m.Groups["submodulePath"].Value };
            }
        }

        private string RunGitCommand(string command, string workTree = null, string gitDir = null)
        {
            // Windows Git requires these to be before the command
            if (workTree != null)
            {
                command = $"--work-tree={workTree} {command}";
            }
            if (gitDir != null)
            {
                command = $"--git-dir={gitDir} {command}";
            }

            var exec = new Exec
            {
                BuildEngine = BuildEngine,
                Command = $"git {command}",
                LogStandardErrorAsError = true,
                ConsoleToMSBuild = true,
            };

            if (!exec.Execute() || exec.ExitCode != 0)
            {
                string error = string.Join(Environment.NewLine, exec.ConsoleOutput.Select(o => o.ItemSpec));
                throw new InvalidOperationException($"git command '{command}' failed with exit code {exec.ExitCode} and error {error ?? "<blank>"}");
            }
            string output = string.Join(Environment.NewLine, exec.ConsoleOutput.Select(o => o.ItemSpec));
            return output.Trim();
        }

        private void HandleSubmodules(string sourceDirPath, string gitDirPath, Dependency dependency)
        {
            var gitModulesPath = Path.Combine(sourceDirPath, ".gitmodules");
            foreach (SubmoduleInfo submodule in GetSubmoduleInfo(gitModulesPath))
            {
                WriteGitCommitMarkerFileForSubmodule(sourceDirPath, gitDirPath, dependency.Sha, submodule.Name, submodule.Path);
            }
        }

        private void WriteGitCommitMarkerFileForSubmodule(string sourceDirPath, string gitDirPath, string parentRepoSha, string submoduleName, string submodulePath)
        {
            var submoduleSha = GetSubmoduleCommit(gitDirPath, parentRepoSha, submodulePath);
            var headDirectory = Path.Combine(sourceDirPath, submodulePath, ".git");
            var headPath = Path.Combine(headDirectory, "HEAD");
            Directory.CreateDirectory(headDirectory);
            File.WriteAllText(headPath, submoduleSha);
        }

        private static void WriteMinimalMetadata(string repoPath, string repoUrl, string hash)
        {
            var fakeGitDirPath = Path.Combine(repoPath, ".git");
            var fakeGitConfigPath = Path.Combine(fakeGitDirPath, "config");
            var fakeGitHeadPath = Path.Combine(fakeGitDirPath, "HEAD");

            Directory.CreateDirectory(fakeGitDirPath);
            File.WriteAllText(fakeGitHeadPath, hash);
            File.WriteAllText(fakeGitConfigPath, $"[remote \"origin\"]{Environment.NewLine}url = \"{repoUrl}\"");
        }


        private string GetSubmoduleCommit(string gitDirPath, string parentRepoSha, string submodulePath)
        {
            var gitObjectList = RunGitCommand($"ls-tree {parentRepoSha} {submodulePath}", gitDir: gitDirPath);
            var submoduleRegex = new Regex(@"\d{6}\s+commit\s+(?<submoduleSha>[a-fA-F0-9]{40})\s+(.+)");
            var submoduleMatch = submoduleRegex.Match(gitObjectList);
            if (!submoduleMatch.Success)
            {
                throw new InvalidDataException($"Couldn't find a submodule commit in {gitObjectList} for {submodulePath}");
            }
            return submoduleMatch.Groups["submoduleSha"].Value;
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

        private class SubmoduleInfo
        {
            internal string Name { get; set; }
            internal string Path { get; set; }
        }
    }
}
