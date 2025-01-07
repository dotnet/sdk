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
    /// Path to the VMR src directory.
    /// </summary>
    [Required]
    public required string SrcPath { get; set; }

    /// <summary>
    /// Path to the usage report to.
    /// </summary>
    [Required]
    public required string OutputPath { get; set; }

    public override bool Execute()
    {
        Log.LogMessage($"Scanning for SBRP Package Usage...");

        ReadSbrpPackages("referencePackages", trackTfms: true);
        ReadSbrpPackages("textOnlyPackages", trackTfms: false);

        ScanProjectReferences();

        GenerateUsageReport();

        return !Log.HasLoggedErrors;
    }

    private void GenerateUsageReport()
    {
        PackageInfo[] existingSbrps = [.. _sbrpPackages.Values.OrderBy(pkg => pkg.Id)];
        PurgeNonReferencedReferences();
        IEnumerable<string> unreferencedSbrps = GetUnreferencedSbrps().Select(pkg => pkg.Path).OrderBy(id => id);
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

    private string GetSbrpPackagesPath(string packageType) => Path.Combine(SbrpRepoSrcPath, packageType, "src");

    private void ReadSbrpPackages(string packageType, bool trackTfms)
    {
        foreach (string projectPath in Directory.GetFiles(GetSbrpPackagesPath(packageType), "*.csproj", SearchOption.AllDirectories))
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
        foreach (string projectJsonFile in Directory.GetFiles(SrcPath, "project.assets.json", SearchOption.AllDirectories))
        {
            LockFile lockFile = new LockFileFormat().Read(projectJsonFile);
            foreach (LockFileTargetLibrary lib in lockFile.Targets.SelectMany(t => t.Libraries))
            {
                if (!_sbrpPackages.TryGetValue(PackageInfo.GetId(lib.Name, lib.Version?.ToString()), out PackageInfo? info))
                {
                    continue;
                }

                if (!info.References.TryGetValue(lockFile.Path, out HashSet<string>? referencedTfms))
                {
                    referencedTfms = [];
                    info.References.Add(lockFile.Path, referencedTfms);
                }

                IEnumerable<string> tfms = lib.CompileTimeAssemblies
                    .Where(asm => asm.Path.StartsWith("lib") || asm.Path.StartsWith("ref"))
                    .Select(asm => asm.Path.Split('/')[1]);
                foreach (string tfm in tfms)
                {
                    referencedTfms.Add(tfm);
                }
            }
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
