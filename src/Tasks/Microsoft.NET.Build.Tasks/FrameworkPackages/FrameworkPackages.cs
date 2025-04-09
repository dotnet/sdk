namespace Microsoft.ComponentDetection.Detectors.NuGet;

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using global::NuGet.Frameworks;
using global::NuGet.Versioning;
using Microsoft.NET.Build.Tasks;
using Microsoft.NET.Build.Tasks.ConflictResolution;

/// <summary>
/// Represents a set of packages that are provided by a specific framework.
/// At the moment this only represents the packages that are provided by the Microsoft.NETCore.App framework.
/// We could extend this to represent the packages provided by other frameworks like Microsoft.AspNetCore.App and Microsoft.WindowsDesktop.App.
/// </summary>
internal sealed partial class FrameworkPackages : IEnumerable<KeyValuePair<string, NuGetVersion>>, IEnumerable
{
    private const string DefaultFrameworkKey = "";
    private static readonly ConcurrentDictionary<NuGetFramework, ConcurrentDictionary<string, FrameworkPackages>> FrameworkPackagesByFramework = [];

    static FrameworkPackages()
    {
        NETStandard20.Register();
        NETStandard21.Register();
        NET461.Register();
        NETCoreApp20.Register();
        NETCoreApp21.Register();
        NETCoreApp22.Register();
        NETCoreApp30.Register();
        NETCoreApp31.Register();
        NETCoreApp50.Register();
        NETCoreApp60.Register();
        NETCoreApp70.Register();
        NETCoreApp80.Register();
        NETCoreApp90.Register();
    }

    public FrameworkPackages(NuGetFramework framework, string frameworkName)
    {
        this.Framework = framework;
        this.FrameworkName = frameworkName;
    }

    public FrameworkPackages(NuGetFramework framework, string frameworkName, FrameworkPackages frameworkPackages)
        : this(framework, frameworkName) => this.Packages = new(frameworkPackages.Packages);

    public NuGetFramework Framework { get; }

    public string FrameworkName { get; }

    public Dictionary<string, NuGetVersion> Packages { get; } = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase);

    private static string GetFrameworkKey(string frameworkName) =>
        frameworkName switch
        {
            FrameworkNames.NetStandardLibrary => DefaultFrameworkKey,
            FrameworkNames.NetCoreApp => DefaultFrameworkKey,
            _ => frameworkName,
        };

    internal static void Register(params FrameworkPackages[] toRegister)
    {
        foreach (var frameworkPackages in toRegister)
        {
            if (!FrameworkPackagesByFramework.TryGetValue(frameworkPackages.Framework, out var frameworkPackagesForVersion))
            {
                FrameworkPackagesByFramework[frameworkPackages.Framework] = frameworkPackagesForVersion = [];
            }

            var frameworkKey = GetFrameworkKey(frameworkPackages.FrameworkName);
            frameworkPackagesForVersion[frameworkKey] = frameworkPackages;
        }
    }

    public static FrameworkPackages[] GetFrameworkPackages(NuGetFramework framework, string[] frameworkReferences, bool acceptNearestMatch = false)
    {
        var frameworkPackages = new List<FrameworkPackages>();

        if (frameworkReferences.Length == 0)
        {
            frameworkReferences = [DefaultFrameworkKey];
        }

        foreach (var frameworkReference in frameworkReferences)
        {
            var frameworkKey = GetFrameworkKey(frameworkReference);

            NuGetFramework nearestFramework;

            if (acceptNearestMatch)
            {
                var reducer = new FrameworkReducer();
                var candidateFrameworks = FrameworkPackagesByFramework.Where(pair => pair.Value.ContainsKey(frameworkKey)).Select(pair => pair.Key);
                nearestFramework = reducer.GetNearest(framework, candidateFrameworks);
            }
            else
            {
                nearestFramework = framework;
            }

            if (FrameworkPackagesByFramework.TryGetValue(nearestFramework, out var frameworkPackagesForVersion) &&
                frameworkPackagesForVersion.TryGetValue(frameworkKey, out var frameworkPackage))
            {
                frameworkPackages.Add(frameworkPackage);
            }
        }

        return frameworkPackages.ToArray();
    }

    public static FrameworkPackages LoadFrameworkPackagesFromPack(Logger log, NuGetFramework framework, string frameworkName, string targetingPackRoot)
    {
        if (framework is null || framework.Framework != FrameworkConstants.FrameworkIdentifiers.NetCoreApp)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(targetingPackRoot))
        {
            var packsFolder = Path.Combine(targetingPackRoot, frameworkName + ".Ref");
            log.LogMessage("Looking for targeting packs in {0}", packsFolder);
            if (Directory.Exists(packsFolder))
            {
                var packVersionPattern = $"{framework.Version.Major}.{framework.Version.Minor}.*";
                var packDirectories = Directory.GetDirectories(packsFolder, packVersionPattern);
                log.LogMessage("Pack directories found: {0}", string.Join(Environment.NewLine, packDirectories));
                var packageOverridesFile = packDirectories
                                                .Select(d => (Overrides: Path.Combine(d, "data", "PackageOverrides.txt"), Version: ParseVersion(Path.GetFileName(d))))
                                                .Where(d => File.Exists(d.Overrides))
                                                .OrderByDescending(d => d.Version)
                                                .FirstOrDefault().Overrides;

                if (packageOverridesFile is not null)
                {
                    log.LogMessage("Found package overrides file {0}", packageOverridesFile);
                    // Adapted from https://github.com/dotnet/sdk/blob/c3a8f72c3a5491c693ff8e49e7406136a12c3040/src/Tasks/Common/ConflictResolution/PackageOverride.cs#L52-L68
                    var packageOverrideLines = File.ReadAllLines(packageOverridesFile);

                    var frameworkPackages = new FrameworkPackages(framework, frameworkName);
                    foreach (var packageOverride in PackageOverride.CreateOverriddenPackages(packageOverrideLines))
                    {
                        frameworkPackages.Packages[packageOverride.Item1] = packageOverride.Item2;
                    }

                    return frameworkPackages;
                }
                else
                {
                    log.LogMessage("No package overrides found in {0}", packsFolder);
                }
            }
            else
            {
                log.LogMessage("Targeting pack folder {0} does not exist", packsFolder);
            }
        }

        return null;

        static NuGetVersion ParseVersion(string versionString) => NuGetVersion.TryParse(versionString, out var version) ? version : null;
    }

    private void Add(string id, string version)
    {
        // intentionally redirect to indexer to allow for overwrite
        this.Packages[id] = NuGetVersion.Parse(version);
    }

    public bool IsAFrameworkComponent(string id, NuGetVersion version) => this.Packages.TryGetValue(id, out var frameworkPackageVersion) && frameworkPackageVersion >= version;

    IEnumerator<KeyValuePair<string, NuGetVersion>> IEnumerable<KeyValuePair<string, NuGetVersion>>.GetEnumerator() => this.Packages.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();

    internal static class FrameworkNames
    {
        public const string AspNetCoreApp = "Microsoft.AspNetCore.App";
        public const string NetCoreApp = "Microsoft.NETCore.App";
        public const string NetStandardLibrary = "NETStandard.Library";
        public const string WindowsDesktopApp = "Microsoft.WindowsDesktop.App";
    }
}
