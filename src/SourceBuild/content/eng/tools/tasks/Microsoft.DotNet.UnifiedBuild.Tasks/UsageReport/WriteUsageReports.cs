// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Microsoft.DotNet.UnifiedBuild.Tasks.UsageReport
{
    public class WriteUsageReports : Task
    {
        private const string SnapshotPrefix = "PackageVersions.";
        private const string SnapshotSuffix = ".Snapshot.props";

        /// <summary>
        /// Source usage data JSON file.
        /// </summary>
        [Required]
        public string DataFile { get; set; }

        /// <summary>
        /// A set of "PackageVersions.{repo}.Snapshot.props" files. They are analyzed to find
        /// packages built during source-build, and which repo built them. This info is added to the
        /// report. New packages are associated to a repo by going through each PVP in ascending
        /// file modification order.
        /// </summary>
        public ITaskItem[] PackageVersionPropsSnapshots { get; set; }

        /// <summary>
        /// Infos that associate packages to the ProdCon build they're from.
        /// 
        /// %(PackageId): Identity of the package.
        /// %(OriginBuildName): Name of the build that produced this package.
        /// </summary>
        public ITaskItem[] ProdConPackageInfos { get; set; }

        /// <summary>
        /// Path to a ProdCon build manifest file (build.xml) as an alternative way to pass
        /// ProdConPackageInfos items.
        /// </summary>
        public string ProdConBuildManifestFile { get; set; }

        /// <summary>
        /// File containing the results of poisoning the prebuilts. Example format:
        /// 
        /// MATCH: output built\dotnet-sdk-...\System.Collections.dll(hash 4b...31) matches one of:
        ///     intermediate\netstandard.library.2.0.1.nupkg\build\...\System.Collections.dll
        /// 
        /// The usage report reads this file, looking for 'intermediate\*.nupkg' to annotate.
        /// </summary>
        public string PoisonedReportFile { get; set; }

        [Required]
        public string OutputDirectory { get; set; }

        public override bool Execute()
        {
            UsageData data = UsageData.Parse(XElement.Parse(File.ReadAllText(DataFile)));

            IEnumerable<RepoOutput> sourceBuildRepoOutputs = GetSourceBuildRepoOutputs();

            // Map package id to the build name that created them in a ProdCon build.
            var prodConPackageOrigin = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (ITaskItem item in ProdConPackageInfos.NullAsEmpty())
            {
                AddProdConPackage(
                    prodConPackageOrigin,
                    item.GetMetadata("PackageId"),
                    item.GetMetadata("OriginBuildName"));
            }

            if (File.Exists(ProdConBuildManifestFile))
            {
                var xml = XElement.Parse(File.ReadAllText(ProdConBuildManifestFile));
                foreach (var x in xml.Descendants("Package"))
                {
                    AddProdConPackage(
                        prodConPackageOrigin,
                        x.Attribute("Id")?.Value,
                        x.Attribute("OriginBuildName")?.Value);
                }
            }

            var poisonNupkgFilenames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(PoisonedReportFile))
            {
                foreach (string line in File.ReadAllLines(PoisonedReportFile))
                {
                    string[] segments = line.Split('\\');
                    if (segments.Length > 2 &&
                        segments[0].Trim() == "intermediate" &&
                        segments[1].EndsWith(".nupkg"))
                    {
                        poisonNupkgFilenames.Add(Path.GetFileNameWithoutExtension(segments[1]));
                    }
                }
            }

            var report = new XElement("AnnotatedUsages");

            var annotatedUsages = data.Usages.NullAsEmpty()
                .Select(usage =>
                {
                    string id = usage.PackageIdentity.Id;
                    string version = usage.PackageIdentity.Version.OriginalVersion;

                    string pvpIdent = WritePackageVersionsProps.GetPropertyName(id, WritePackageVersionsProps.VersionPropertySuffix);

                    var sourceBuildCreator = new StringBuilder();
                    foreach (RepoOutput output in sourceBuildRepoOutputs)
                    {
                        foreach (PackageVersionPropsElement p in output.Built)
                        {
                            if (p.Name.Equals(pvpIdent, StringComparison.OrdinalIgnoreCase))
                            {
                                if (sourceBuildCreator.Length != 0)
                                {
                                    sourceBuildCreator.Append(" ");
                                }
                                sourceBuildCreator.Append(output.Repo);
                                sourceBuildCreator.Append(" ");
                                sourceBuildCreator.Append(p.Name);
                                sourceBuildCreator.Append("/");
                                sourceBuildCreator.Append(p.Version);
                            }
                        }
                    }

                    prodConPackageOrigin.TryGetValue(id, out string prodConCreator);

                    return new AnnotatedUsage
                    {
                        Usage = usage,

                        Project = data.ProjectDirectories
                            ?.FirstOrDefault(p => usage.AssetsFile?.StartsWith(p) ?? false),

                        SourceBuildPackageIdCreator = sourceBuildCreator.Length == 0
                            ? null
                            : sourceBuildCreator.ToString(),

                        ProdConPackageIdCreator = prodConCreator,

                        TestProjectByHeuristic = IsTestUsageByHeuristic(usage),

                        EndsUpInOutput = poisonNupkgFilenames.Contains($"{id}.{version}")
                    };
                })
                .ToArray();

            foreach (var onlyTestProjectUsage in annotatedUsages
                .GroupBy(u => u.Usage.PackageIdentity)
                .Where(g => g.All(u => u.TestProjectByHeuristic))
                .SelectMany(g => g))
            {
                onlyTestProjectUsage.TestProjectOnlyByHeuristic = true;
            }

            report.Add(annotatedUsages.Select(u => u.ToXml()));

            Directory.CreateDirectory(OutputDirectory);

            File.WriteAllText(
                Path.Combine(OutputDirectory, "annotated-usage.xml"),
                report.ToString());

            return !Log.HasLoggedErrors;
        }

        private RepoOutput[] GetSourceBuildRepoOutputs()
        {
            var pvpSnapshotFiles = PackageVersionPropsSnapshots.NullAsEmpty()
                .Select(item =>
                {
                    var content = File.ReadAllText(item.ItemSpec);
                    return new
                    {
                        Path = item.ItemSpec,
                        Content = content,
                        Xml = XElement.Parse(content)
                    };
                })
                .OrderBy(snapshot =>
                {
                    // Get the embedded creation time if possible: the file's original metadata may
                    // have been destroyed by copying, zipping, etc.
                    string creationTime = snapshot.Xml
                        // Get all elements
                        .Elements()
                        // Select all the subelements
                        .SelectMany(e => e.Elements())
                        // Find all that match the creation time property name
                        .Where(e => e.Name == snapshot.Xml.GetDefaultNamespace().GetName(WritePackageVersionsProps.CreationTimePropertyName))
                        // There should be only one or zero
                        .SingleOrDefault()?.Value;

                    if (string.IsNullOrEmpty(creationTime))
                    {
                        Log.LogError($"No creation time property found in snapshot {snapshot.Path}");
                        return default(DateTime);
                    }

                    return new DateTime(long.Parse(creationTime));
                })
                .Select(snapshot =>
                {
                    string filename = Path.GetFileName(snapshot.Path);
                    return new
                    {
                        Repo = filename.Substring(
                            SnapshotPrefix.Length,
                            filename.Length - SnapshotPrefix.Length - SnapshotSuffix.Length),
                        PackageVersionProp = PackageVersionPropsElement.Parse(snapshot.Xml)
                    };
                })
                .ToArray();

            return pvpSnapshotFiles.Skip(1)
                .Zip(pvpSnapshotFiles, (pvp, prev) => new RepoOutput
                {
                    Repo = prev.Repo,
                    Built = pvp.PackageVersionProp.Except(prev.PackageVersionProp).ToArray()
                })
                .ToArray();
        }

        private void AddProdConPackage(
            Dictionary<string, string> packageOrigin,
            string id,
            string origin)
        {
            if (!string.IsNullOrEmpty(id) &&
                !string.IsNullOrEmpty(origin))
            {
                packageOrigin[id] = origin;
            }
        }

        public static bool IsTestUsageByHeuristic(Usage usage)
        {
            string[] assetsFileParts = usage.AssetsFile?.Split('/', '\\');

            // If the dir name ends in Test(s), it's probably a test project.
            // Ignore the first two segments to avoid classifying everything in "src/vstest".
            // This also catches "test" dirs that contain many test projects.
            if (assetsFileParts?.Skip(2).Any(p =>
                p.EndsWith("Tests", StringComparison.OrdinalIgnoreCase) ||
                p.EndsWith("Test", StringComparison.OrdinalIgnoreCase)) == true)
            {
                return true;
            }

            // CoreFX restores test dependencies during this sync project.
            if (assetsFileParts?.Contains("XUnit.Runtime") == true)
            {
                return true;
            }

            return false;
        }

        private class RepoOutput
        {
            public string Repo { get; set; }
            public PackageVersionPropsElement[] Built { get; set; }
        }

        private struct PackageVersionPropsElement
        {
            public static PackageVersionPropsElement[] Parse(XElement xml)
            {
                return xml
                    // Get the first PropertyGroup. The second PropertyGroup is 'extra properties', and the third group is the creation time.
                    // Only select the first because the extra properties are not built packages.
                    .Elements()
                    .First()
                    // Get all *PackageVersion property elements.
                    .Elements()
                    .Select(x => new PackageVersionPropsElement(
                        x.Name.LocalName,
                        x.Nodes().OfType<XText>().First().Value))
                    .ToArray();
            }

            public string Name { get; }
            public string Version { get; }

            public PackageVersionPropsElement(string name, string version)
            {
                Name = name;
                Version = version;
            }
        }
    }
}
