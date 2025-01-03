// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Task = Microsoft.Build.Utilities.Task;

namespace Microsoft.DotNet.UnifiedBuild.Tasks.UsageReport
{
    public class WritePackageUsageData : Task
    {
        public string[] RestoredPackageFiles { get; set; }
        public string[] TarballPrebuiltPackageFiles { get; set; }
        public string[] ReferencePackageFiles { get; set; }
        public string[] SourceBuiltPackageFiles { get; set; }

        /// <summary>
        /// Specific PackageInfo items to check for usage. An alternative to passing lists of nupkgs
        /// when the nupkgs have already been parsed to get package info items.
        ///
        /// %(Identity): Path to the original nupkg.
        /// %(PackageId): Identity of the package.
        /// %(PackageVersion): Version of the package.
        /// </summary>
        public ITaskItem[] NuGetPackageInfos { get; set; }

        /// <summary>
        /// runtime.json files (from Microsoft.NETCore.Platforms) to use to look for the set of all
        /// possible runtimes. This is used to determine which part of a package id is its
        /// 'runtime.{rid}.' prefix, if it has the prefix.
        /// </summary>
        public string[] PlatformsRuntimeJsonFiles { get; set; }

        /// <summary>
        /// Keep track of the RID built that caused these usages.
        /// </summary>
        public string TargetRid { get; set; }

        /// <summary>
        /// Project directories to scan for project.assets.json files. If these directories contain
        /// one another, the project.assets.json files is reported as belonging to the first project
        /// directory that contains it. For useful results, put the leafmost directories first.
        ///
        /// This isn't used here, but it's included in the usage data so report generation can
        /// happen independently of commits that add/remove submodules.
        /// </summary>
        public string[] ProjectDirectories { get; set; }

        /// <summary>
        /// A root dir that contains all ProjectDirectories. This is used to find the relative path
        /// of each usage.
        /// </summary>
        [Required]
        public string RootDir { get; set; }

        /// <summary>
        /// project.assets.json files to ignore, for example, because they are checked-in assets not
        /// generated during source-build and cause false positives.
        /// </summary>
        public string[] IgnoredProjectAssetsJsonFiles { get; set; }

        /// <summary>
        /// Output usage data JSON file path.
        /// </summary>
        [Required]
        public string DataFile { get; set; }

        /// <summary>
        /// If passed, the path of the archive file to generate that includes a copy of all
        /// project.asset.json files found.
        /// </summary>
        public string ProjectAssetsJsonArchiveFile { get; set; }

        public override bool Execute()
        {
            DateTime startTime = DateTime.Now;
            Log.LogMessage(MessageImportance.High, "Writing package usage data...");

            string[] projectDirectoriesOutsideRoot = ProjectDirectories.NullAsEmpty()
                .Where(dir => !dir.StartsWith(RootDir, StringComparison.Ordinal))
                .ToArray();

            if (projectDirectoriesOutsideRoot.Any())
            {
                throw new ArgumentException(
                    $"All ProjectDirectories must be in RootDir '{RootDir}', but found " +
                    string.Join(", ", projectDirectoriesOutsideRoot));
            }

            Log.LogMessage(MessageImportance.Low, "Finding set of RIDs...");

            string[] possibleRids = PlatformsRuntimeJsonFiles.NullAsEmpty()
                .SelectMany(ReadRidsFromRuntimeJson)
                .Distinct()
                .ToArray();

            Log.LogMessage(MessageImportance.Low, "Reading package identities...");

            PackageIdentity[] restored = RestoredPackageFiles.NullAsEmpty()
                .Select(ReadNuGetPackageInfos.ReadIdentity)
                .Distinct()
                .ToArray();

            PackageIdentity[] tarballPrebuilt = TarballPrebuiltPackageFiles.NullAsEmpty()
                .Select(ReadNuGetPackageInfos.ReadIdentity)
                .Distinct()
                .ToArray();

            PackageIdentity[] referencePackages = ReferencePackageFiles.NullAsEmpty()
                .Select(ReadNuGetPackageInfos.ReadIdentity)
                .Distinct()
                .ToArray();

            PackageIdentity[] sourceBuilt = SourceBuiltPackageFiles.NullAsEmpty()
                .Select(ReadNuGetPackageInfos.ReadIdentity)
                .Distinct()
                .ToArray();

            IEnumerable<PackageIdentity> prebuilt = restored.Except(sourceBuilt).Except(referencePackages);

            PackageIdentity[] toCheck = NuGetPackageInfos.NullAsEmpty()
                .Select(item => new PackageIdentity(
                    item.GetMetadata("PackageId"),
                    NuGetVersion.Parse(item.GetMetadata("PackageVersion"))))
                .Concat(prebuilt)
                .ToArray();

            Log.LogMessage(MessageImportance.Low, "Finding project.assets.json files...");

            string[] assetFiles = Directory
                .GetFiles(RootDir, "project.assets.json", SearchOption.AllDirectories)
                .Select(GetPathRelativeToRoot)
                .Except(IgnoredProjectAssetsJsonFiles.NullAsEmpty().Select(GetPathRelativeToRoot))
                .ToArray();

            if (!string.IsNullOrEmpty(ProjectAssetsJsonArchiveFile))
            {
                Log.LogMessage(MessageImportance.Low, "Archiving project.assets.json files...");

                Directory.CreateDirectory(Path.GetDirectoryName(ProjectAssetsJsonArchiveFile));

                using (var projectAssetArchive = new ZipArchive(
                    File.Open(
                        ProjectAssetsJsonArchiveFile,
                        FileMode.Create,
                        FileAccess.ReadWrite),
                    ZipArchiveMode.Create))
                {
                    // Only one entry can be open at a time, so don't do this during the Parallel
                    // ForEach later.
                    foreach (var relativePath in assetFiles)
                    {
                        using (var stream = File.OpenRead(Path.Combine(RootDir, relativePath)))
                        using (Stream entryWriter = projectAssetArchive
                            .CreateEntry(relativePath, CompressionLevel.Optimal)
                            .Open())
                        {
                            stream.CopyTo(entryWriter);
                        }
                    }
                }
            }

            Log.LogMessage(MessageImportance.Low, "Reading usage info...");

            var usages = new ConcurrentBag<Usage>();

            Parallel.ForEach(
                assetFiles,
                assetFile =>
                {
                    JObject jObj;

                    using (var file = File.OpenRead(Path.Combine(RootDir, assetFile)))
                    using (var reader = new StreamReader(file))
                    using (var jsonReader = new JsonTextReader(reader))
                    {
                        jObj = (JObject)JToken.ReadFrom(jsonReader);
                    }

                    var properties = new HashSet<string>(
                        jObj.SelectTokens("$.targets.*").Children()
                            .Concat(jObj.SelectToken("$.libraries"))
                            .Select(t => ((JProperty)t).Name)
                            .Distinct(), 
                        StringComparer.OrdinalIgnoreCase);

                    var directDependencies = jObj.SelectTokens("$.project.frameworks.*.dependencies").Children().Select(dep =>
                        new
                        {
                            name = ((JProperty)dep).Name,
                            target = dep.SelectToken("$..target")?.ToString(),
                            version = VersionRange.Parse(dep.SelectToken("$..version")?.ToString()),
                            autoReferenced = dep.SelectToken("$..autoReferenced")?.ToString() == "True",
                        })
                        .ToArray();

                    foreach (var identity in toCheck
                        .Where(id => properties.Contains(id.Id + "/" + id.Version.OriginalVersion)))
                    {
                        var directDependency =
                            directDependencies?.FirstOrDefault(
                                d => d.name == identity.Id && 
                                     d.version.Satisfies(identity.Version));
                        usages.Add(Usage.Create(
                            assetFile,
                            identity,
                            directDependency != null,
                            directDependency?.autoReferenced == true,
                            possibleRids));
                    }
                });

            Log.LogMessage(MessageImportance.Low, "Searching for unused packages...");

            foreach (PackageIdentity restoredWithoutUsagesFound in
                toCheck.Except(usages.Select(u => u.PackageIdentity)))
            {
                usages.Add(Usage.Create(
                    null,
                    restoredWithoutUsagesFound,
                    false,
                    false,
                    possibleRids));
            }

            // Packages that were included in the tarball as prebuilts, but weren't even restored.
            PackageIdentity[] neverRestoredTarballPrebuilts = tarballPrebuilt
                .Except(restored)
                .ToArray();

            Log.LogMessage(MessageImportance.Low, $"Writing data to '{DataFile}'...");

            var data = new UsageData
            {
                CreatedByRid = TargetRid,
                Usages = usages.ToArray(),
                NeverRestoredTarballPrebuilts = neverRestoredTarballPrebuilts,
                ProjectDirectories = ProjectDirectories
                    ?.Select(GetPathRelativeToRoot)
                    .ToArray()
            };

            Directory.CreateDirectory(Path.GetDirectoryName(DataFile));
            File.WriteAllText(DataFile, data.ToXml().ToString());

            Log.LogMessage(
                MessageImportance.High,
                $"Writing package usage data... done. Took {DateTime.Now - startTime}");

            return !Log.HasLoggedErrors;
        }

        private string GetPathRelativeToRoot(string path)
        {
            if (path.StartsWith(RootDir))
            {
                return path.Substring(RootDir.Length).Replace(Path.DirectorySeparatorChar, '/');
            }

            throw new ArgumentException($"Path '{path}' is not within RootDir '{RootDir}'");
        }

        private static string[] ReadRidsFromRuntimeJson(string path)
        {
            var root = JObject.Parse(File.ReadAllText(path));
            return root["runtimes"]
                .Values<JProperty>()
                .Select(o => o.Name)
                .ToArray();
        }
    }
}
