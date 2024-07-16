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
/// 1. Unreferenced packages
/// 2. Unreferenced TFMs
/// </summary>
public class WriteSbrpUsageReport : Task
{
    private const string SbrpRepoName = "source-build-reference-packages";

    private readonly Dictionary<string, PackageInfo> _sbrpPackages = [];

    /// <summary>
    /// Path to the VMR src directory.
    /// </summary>
    [Required]
    public string SrcPath { get; set; }

    /// <summary>
    /// Path to the usage report to.
    /// </summary>
    [Required]
    public string OutputPath { get; set; }

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
        Report report = new();
        report.Sbrps = [.. _sbrpPackages.Values.OrderBy(pkg => pkg.Id)];
        PurgeNonReferencedReferences();
        report.UnreferencedSbrps = [.. GetUnreferencedSbrps().Select(pkg => pkg.Id).OrderBy(id => id)];

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
            PackageInfo[] unrefPkgs = GetUnreferencedSbrps()
                .Select(pkg => pkg)
                .ToArray();

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

    private string GetSbrpPackagesPath(string packageType) =>
        Path.Combine(SrcPath, SbrpRepoName, "src", packageType, "src");

    private void ReadSbrpPackages(string packageType, bool trackTfms)
    {
        EnumerationOptions options = new() { RecurseSubdirectories = true };

        foreach (string projectPath in Directory.GetFiles(GetSbrpPackagesPath(packageType), "*.csproj", options))
        {
            DirectoryInfo directory = Directory.GetParent(projectPath);
            string version = directory.Name;
            string projectName = Path.GetFileNameWithoutExtension(projectPath);
            PackageInfo info = new()
            {
                Version = version,
                Name = projectName[..(projectName.Length - 1 - version.Length)],
                Path = directory.FullName,
            };

            if (trackTfms)
            {
                XDocument xmlDoc = XDocument.Load(projectPath);
                // Reference packages are generated using the TargetFrameworks property
                // so there is no need to handle the TargetFramework property.
                string[] tfms = xmlDoc.Element("Project")?
                    .Elements("PropertyGroup")
                    .Elements("TargetFrameworks")
                    .FirstOrDefault()?.Value?.Split(';');

                if (tfms == null || tfms.Length == 0)
                {
                    Log.LogError($"No TargetFrameworks were delected in {projectPath}.");
                }

                info.Tfms = new HashSet<string>(tfms);
            }
            else
            {
                info.Tfms = [];
            }

            _sbrpPackages.Add(info.Id, info);
            Log.LogMessage($"Detected package: {info.Id}");
        }
    }

    private void ScanProjectReferences()
    {
        EnumerationOptions options = new() { RecurseSubdirectories = true };

        foreach (string projectJsonFile in Directory.GetFiles(SrcPath, "project.assets.json", options))
        {
            LockFile lockFile = new LockFileFormat().Read(projectJsonFile);
            foreach (LockFileTargetLibrary lib in lockFile.Targets.SelectMany(t => t.Libraries))
            {
                if (!_sbrpPackages.TryGetValue($"{lib.Name}/{lib.Version}", out PackageInfo info))
                {
                    continue;
                }

                if (!info.References.TryGetValue(lockFile.Path, out HashSet<string> referencedTfms))
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

    private class PackageInfo
    {
        public string Id => $"{Name}/{Version}";
        public string Name { get; set; }
        public string Version { get; set; }
        public string Path { get; set; }
        public HashSet<string> Tfms { get; set; }

        // Dictionary of projects referencing the SBRP and the TFMs referenced by each project
        public Dictionary<string, HashSet<string>> References { get; } = [];
    }

    private class Report
    {
        public PackageInfo[] Sbrps { get; set; }
        public string[] UnreferencedSbrps { get; set; }
    }
}
