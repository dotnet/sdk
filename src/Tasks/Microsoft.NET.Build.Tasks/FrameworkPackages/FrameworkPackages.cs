namespace Microsoft.ComponentDetection.Detectors.NuGet;

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using global::NuGet.Frameworks;
using global::NuGet.Versioning;

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

    public static FrameworkPackages[] GetFrameworkPackages(NuGetFramework framework, string[] frameworkReferences, string targetingPackRoot)
    {
        var frameworkPackages = new List<FrameworkPackages>();

        foreach (var frameworkReference in frameworkReferences)
        {
            var frameworkKey = GetFrameworkKey(frameworkReference);
            if (FrameworkPackagesByFramework.TryGetValue(framework, out var frameworkPackagesForVersion) &&
                frameworkPackagesForVersion.TryGetValue(frameworkKey, out var frameworkPackage))
            {
                frameworkPackages.Add(frameworkPackage);
            }
            else
            {
                // if we didn't predefine the package overrides, load them from the targeting pack
                // we might just leave this out since in future frameworks we'll have this functionality built into NuGet.Frameworks
                var frameworkPackagesFromPack = LoadFrameworkPackagesFromPack(framework, frameworkReference, targetingPackRoot) ?? new FrameworkPackages(framework, frameworkReference);

                Register(frameworkPackagesFromPack);

                frameworkPackages.Add(frameworkPackagesFromPack);
            }
        }

        return frameworkPackages.ToArray();
    }

    private static FrameworkPackages LoadFrameworkPackagesFromPack(NuGetFramework framework, string frameworkName, string targetingPackRoot)
    {
        if (framework is null || framework.Framework != FrameworkConstants.FrameworkIdentifiers.NetCoreApp)
        {
            return null;
        }

        var reducer = new FrameworkReducer();
        var frameworkKey = GetFrameworkKey(frameworkName);
        var candidateFrameworks = FrameworkPackagesByFramework.Where(pair => pair.Value.ContainsKey(frameworkKey)).Select(pair => pair.Key);
        var nearestFramework = reducer.GetNearest(framework, candidateFrameworks);

        var frameworkPackages = nearestFramework is null ?
            new FrameworkPackages(framework, frameworkName) :
            new FrameworkPackages(framework, frameworkName, FrameworkPackagesByFramework[nearestFramework][frameworkKey]);

        if (!string.IsNullOrEmpty(targetingPackRoot))
        {
            var packsFolder = Path.Combine(targetingPackRoot, frameworkName + ".Ref");
            if (Directory.Exists(packsFolder))
            {
                var packVersionPattern = $"{framework.Version.Major}.{framework.Version.Minor}.*";
                var packDirectories = Directory.GetDirectories(packsFolder, packVersionPattern);
                var packageOverridesFile = packDirectories
                                                .Select(d => (Overrides: Path.Combine(d, "data", "PackageOverrides.txt"), Version: ParseVersion(Path.GetFileName(d))))
                                                .Where(d => File.Exists(d.Overrides))
                                                .OrderByDescending(d => d.Version)
                                                .FirstOrDefault().Overrides;

                if (packageOverridesFile is not null)
                {
                    // Adapted from https://github.com/dotnet/sdk/blob/c3a8f72c3a5491c693ff8e49e7406136a12c3040/src/Tasks/Common/ConflictResolution/PackageOverride.cs#L52-L68
                    var packageOverrides = File.ReadAllLines(packageOverridesFile);

                    foreach (var packageOverride in packageOverrides)
                    {
                        var packageOverrideParts = packageOverride.Trim().Split('|');

                        if (packageOverrideParts.Length == 2)
                        {
                            var packageId = packageOverrideParts[0];
                            var packageVersion = ParseVersion(packageOverrideParts[1]);

                            frameworkPackages.Packages[packageId] = packageVersion;
                        }
                    }
                }
            }
        }

        return frameworkPackages;

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
