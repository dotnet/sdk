// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.LibraryModel;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.UnifiedBuild.Tasks;

/// <summary>
/// Reports the usage of the source-build-reference-packages:
/// 1. SBRP references
/// 2. Unreferenced packages
/// </summary>
public class WriteSbrpUsageReport : Task
{
    private const string SbrpRepoName = "source-build-reference-packages";

    private readonly Dictionary<string, PackageInfo> _sbrpPackages = [];

    /// <summary>
    /// Path to the SBRP src directory.
    /// </summary>
    [Required]
    public required string SbrpRepoSrcPath { get; set; }

    /// <summary>
    /// Paths to the project.assets.json files produced by the build.
    ///
    /// %(Identity): project.assets.json file path.
    /// </summary>
    [Required]
    public required ITaskItem[] ProjectAssetsJsons { get; set; }

    /// <summary>
    /// Path to the usage report to.
    /// </summary>
    [Required]
    public required string OutputPath { get; set; }

    public override bool Execute()
    {
        Log.LogMessage($"Scanning for SBRP Package Usage...");

        ReadSbrpPackages(Path.Combine("referencePackages", "src"), trackTfms: true);
        ReadSbrpPackages(Path.Combine("targetPacks", "ILsrc"), trackTfms: false);
        ReadSbrpPackages(Path.Combine("textOnlyPackages", "src"), trackTfms: false);

        ScanProjectReferences();

        GenerateUsageReport();

        return !Log.HasLoggedErrors;
    }

    private void GenerateUsageReport()
    {
        PackageInfo[] existingSbrps = [.. _sbrpPackages.Values.OrderBy(pkg => pkg.Id)];
        PurgeNonReferencedReferences();
        IEnumerable<string> unreferencedSbrps = GetUnreferencedSbrps().Select(pkg => pkg.Path).OrderBy(id => id);

        if (unreferencedSbrps.Count() == existingSbrps.Length)
        {
            Log.LogError("No SBRP packages are detected as being referenced.");
        }

        Report report = new(existingSbrps, unreferencedSbrps);

        string reportFilePath = Path.Combine(OutputPath, "sbrpPackageUsage.json");
#pragma warning disable CA1869 // Cache and reuse 'JsonSerializerOptions' instances
        string jsonContent = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
#pragma warning restore CA1869 // Cache and reuse 'JsonSerializerOptions' instances
        File.WriteAllText(reportFilePath, jsonContent);
    }

    /// <summary>
    /// Removes all references from unreferenced SBRP packages. This is necessary to determine the
    /// complete set of unreferenced SBRP packages.
    /// </summary>
    private void PurgeNonReferencedReferences()
    {
        bool hasPurged;
        do
        {
            hasPurged = false;
            PackageInfo[] unrefPkgs = GetUnreferencedSbrps().ToArray();

            foreach (PackageInfo sbrpPkg in _sbrpPackages.Values)
            {
                foreach (PackageInfo unrefPkg in unrefPkgs)
                {
                    var unref = sbrpPkg.References.Keys
                        .SingleOrDefault(path => path.Contains(SbrpRepoName) && path.Contains($"{unrefPkg.Name}.{unrefPkg.Version}"));
                    if (unref != null)
                    {
                        Log.LogMessage($"Removing {unrefPkg.Id} from {sbrpPkg.Id}'s references.");
                        sbrpPkg.References.Remove(unref);
                        hasPurged = true;
                    }
                }
            }
        } while (hasPurged);
    }

    private IEnumerable<PackageInfo> GetUnreferencedSbrps() =>
        _sbrpPackages.Values.Where(pkg => pkg.References.Count == 0);

    private void ReadSbrpPackages(string packageSrcRelativePath, bool trackTfms)
    {
        string packageSrcPath = Path.Combine(SbrpRepoSrcPath, packageSrcRelativePath);
        foreach (string projectPath in Directory.GetFiles(packageSrcPath, "*.csproj", SearchOption.AllDirectories))
        {
            DirectoryInfo? directory = Directory.GetParent(projectPath);
            string version = directory!.Name;
            string projectName = Path.GetFileNameWithoutExtension(projectPath);
            HashSet<string>? tfms = null;

            if (trackTfms)
            {
                XDocument xmlDoc = XDocument.Load(projectPath);
                // Reference packages are generated using the TargetFrameworks property
                // so there is no need to handle the TargetFramework property.
                tfms = xmlDoc.Element("Project")?
                    .Elements("PropertyGroup")
                    .Elements("TargetFrameworks")
                    .FirstOrDefault()?.Value?
                    .Split(';')
                    .ToHashSet();

                if (tfms == null || tfms.Count == 0)
                {
                    Log.LogError($"No TargetFrameworks were detected in {projectPath}.");
                    continue;
                }
            }

            PackageInfo info = new(projectName[..(projectName.Length - 1 - version.Length)],
                version,
                directory.FullName,
                tfms);

            _sbrpPackages.Add(info.Id, info);
            Log.LogMessage($"Detected package: {info.Id}");
        }
    }

    private void ScanProjectReferences()
    {
        if (ProjectAssetsJsons.Length == 0)
        {
            Log.LogError($"No project.assets.json files were specified.");
            return;
        }

        foreach (string projectJsonFile in ProjectAssetsJsons.Select(item => item.GetMetadata("Identity")))
        {
            LockFile lockFile = new LockFileFormat().Read(projectJsonFile);
            foreach (LockFileTargetLibrary lib in lockFile.Targets.SelectMany(t => t.Libraries))
            {
                IEnumerable<string> tfms = lib.CompileTimeAssemblies
                    .Where(asm => asm.Path.StartsWith("lib") || asm.Path.StartsWith("ref"))
                    .Select(asm => asm.Path.Split('/')[1]);

                TrackPackageReference(lockFile.Path, lib.Name, lib.Version?.ToString(), tfms);
            }

            foreach (DownloadDependency downloadDep in lockFile.PackageSpec.TargetFrameworks.SelectMany(fx => fx.DownloadDependencies))
            {
                TrackPackageReference(lockFile.Path, downloadDep.Name, downloadDep.VersionRange.MinVersion?.ToString(), Enumerable.Empty<string>());
            }

            if (lockFile.PackageSpec.RestoreMetadata.ProjectPath.Contains(SbrpRepoName))
            {
                // For SBRP projects, we need to track the project references as well. While project references are included in the targets
                // which were processed above, only the resolved version is included in the cases when the dependency graph contains multiple
                // versions. All project references must be tracked as dependencies because they are required to build SBRP.
                foreach (ProjectRestoreMetadataFrameworkInfo targetFx in lockFile.PackageSpec.RestoreMetadata.TargetFrameworks)
                {
                    foreach (ProjectRestoreReference projectRef in targetFx.ProjectReferences)
                    {
                        if (projectRef.ProjectPath.Contains(SbrpRepoName))
                        {
                            string[] pathSegments = projectRef.ProjectPath.Split('/');
                            string projName = pathSegments[pathSegments.Length - 3];
                            string projVersion = pathSegments[pathSegments.Length - 2];
                            TrackPackageReference(lockFile.Path, projName, projVersion, new[] { targetFx.TargetAlias });
                        }
                        else
                        {
                            Log.LogMessage($"Unexpected non-SBRP project reference detected: {projectRef.ProjectPath}");
                        }
                    }
                }
            }
        }
    }

    private void TrackPackageReference(string lockFilePath, string? name, string? version, IEnumerable<string> tfms)
    {
        string id = PackageInfo.GetId(name, version);
        if (!_sbrpPackages.TryGetValue(id, out PackageInfo? info))
        {
            return;
        }

        if (!info.References.TryGetValue(lockFilePath, out HashSet<string>? referencedTfms))
        {
            referencedTfms = [];
            info.References.Add(lockFilePath, referencedTfms);
        }

        foreach (string tfm in tfms)
        {
            referencedTfms!.Add(tfm);
        }
    }

    private record PackageInfo(string Name, string Version, string Path, HashSet<string>? Tfms = default)
    {
        public string Id => GetId(Name, Version);

        // Dictionary of projects referencing the SBRP and the TFMs referenced by each project
        public Dictionary<string, HashSet<string>> References { get; } = [];

        public static string GetId(string? Name, string? Version) => $"{Name?.ToLowerInvariant()}/{Version}";
    }

    private record Report(IEnumerable<PackageInfo> Sbrps, IEnumerable<string> UnreferencedSbrps);
}
