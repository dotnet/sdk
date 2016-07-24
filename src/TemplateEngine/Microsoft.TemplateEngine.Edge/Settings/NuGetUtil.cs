using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public static class NuGetUtil
    {
        private static readonly List<SourceRepository> Repos = new List<SourceRepository>();
        private static readonly object Sync = new object();
        private static CachingSourceProvider _cachingSourceProvider;
        private static bool _inited;

        public static async Task<string> GetCurrentVersionOfPackageAsync(string packageId, string currentVersion)
        {
            try
            {
                NuGetVersion maxVersion = NuGetVersion.Parse(currentVersion);
                bool updated = false;

                foreach (SourceRepository repo in Repos)
                {
                    FindPackageByIdResource resource = await repo.GetResourceAsync<FindPackageByIdResource>();

                    if (resource == null)
                    {
                        continue;
                    }

                    try
                    {
                        IReadOnlyList<NuGetVersion> versions = (await resource.GetAllVersionsAsync(packageId, CancellationToken.None))?.ToList();

                        if (versions == null || versions.Count == 0)
                        {
                            continue;
                        }

                        NuGetVersion maxVer = versions.Max();
                        if (maxVer.CompareTo(maxVersion) > 0)
                        {
                            updated = true;
                            maxVersion = maxVer;
                        }
                    }
                    catch (FatalProtocolException)
                    {
                    }
                }

                return updated ? maxVersion.ToString() : null;
            }
            catch
            {
                return null;
            }
        }

        public static void Init()
        {
            if (_inited)
            {
                return;
            }

            lock (Sync)
            {
                if (_inited)
                {
                    return;
                }

                string basepath = NuGetEnvironment.GetFolderPath(NuGetFolderPath.MachineWideConfigDirectory);
                IEnumerable<NuGet.Configuration.Settings> settingses = NuGet.Configuration.Settings.LoadMachineWideSettings(basepath);

                foreach (NuGet.Configuration.Settings settings in settingses)
                {
                    Init(settings);
                }

                settingses = NuGet.Configuration.Settings.LoadMachineWideSettings(Paths.Global.BaseDir);

                foreach (NuGet.Configuration.Settings settings in settingses)
                {
                    Init(settings);
                }

                settingses = NuGet.Configuration.Settings.LoadMachineWideSettings(Paths.User.BaseDir);

                foreach (NuGet.Configuration.Settings settings in settingses)
                {
                    Init(settings);
                }

                _inited = true;
            }
        }

        public static void InstallPackage(IReadOnlyList<string> packages, bool quiet)
        {
            Init();
            RemoteWalkContext context = new RemoteWalkContext();

            ILogger logger = new NullLogger();
            SourceCacheContext cacheContext = new SourceCacheContext
            {
                IgnoreFailedSources = true
            };

            foreach (SourceRepository repo in Repos)
            {
                if (!repo.PackageSource.IsLocal)
                {
                    context.RemoteLibraryProviders.Add(new SourceRepositoryDependencyProvider(repo, logger, cacheContext));
                }
                else
                {
                    context.LocalLibraryProviders.Add(new SourceRepositoryDependencyProvider(repo, logger, cacheContext));
                }
            }

            Paths.User.Content.CreateDirectory();
            RemoteDependencyWalker walker = new RemoteDependencyWalker(context);
            HashSet<Package> remainingPackages = new HashSet<Package>(packages.Select(x => new Package(x, VersionRange.All)));
            HashSet<Package> encounteredPackages = new HashSet<Package>();
            List<string> templateRoots = new List<string>();
            List<KeyValuePair<string, string>> componentRoots = new List<KeyValuePair<string, string>>();

            while (remainingPackages.Count > 0)
            {
                HashSet<Package> nextRound = new HashSet<Package>();

                foreach (Package package in remainingPackages)
                {
                    string name = package.PackageId;
                    GraphNode<RemoteResolveResult> result = walker.WalkAsync(new LibraryRange(name, package.Version, LibraryDependencyTarget.All), NuGetFramework.AnyFramework, "", RuntimeGraph.Empty, true).Result;
                    RemoteMatch match = result.Item.Data.Match;
                    PackageIdentity packageIdentity = new PackageIdentity(match.Library.Name, match.Library.Version);

                    nextRound.UnionWith(result.Item.Data.Dependencies.Select(x => new Package(x.Name, x.LibraryRange.VersionRange)));

                    VersionFolderPathContext versionFolderPathContext = new VersionFolderPathContext(
                        packageIdentity,
                        Paths.User.PackageCache,
                        new NullLogger(),
                        packageSaveMode: PackageSaveMode.Defaultv3,
                        xmlDocFileSaveMode: XmlDocFileSaveMode.Skip,
                        fixNuspecIdCasing: true,
                        normalizeFileNames: true);

                    string source = Path.Combine(Paths.User.PackageCache, match.Library.Name, match.Library.Version.ToString());

                    if (!source.Exists() && match.Provider != null)
                    {
                        PackageExtractor.InstallFromSourceAsync(
                            stream => match.Provider.CopyToAsync(match.Library, stream, CancellationToken.None),
                            versionFolderPathContext,
                            CancellationToken.None).Wait();

                        string target = Path.Combine(Paths.User.Content, match.Library.Name);
                        target.CreateDirectory();
                        target = Path.Combine(target, match.Library.Version.ToString());
                        target.CreateDirectory();
                        source.Copy(target);
                        target.Delete("*.nupkg", "*.nupkg.*");

                        string nuspec = target.EnumerateFiles("*.nuspec").FirstOrDefault();

                        //If there's a nuspec, figure out whether this package is a template and walk the dependency graph
                        if (nuspec?.Exists() ?? false)
                        {
                            XDocument doc = XDocument.Load(nuspec);
                            IReadOnlyList<PackageType> types = NuspecUtility.GetPackageTypes(doc.Root.Element(XName.Get("metadata", "http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd")), false);
                            //If the thing we got is a template...
                            if (types.Any(x => string.Equals(x.Name, "template", StringComparison.OrdinalIgnoreCase)))
                            {
                                templateRoots.Add(target);
                            }
                            else
                            {
                                componentRoots.Add(new KeyValuePair<string, string>(match.Library.Name, match.Library.Version.ToString()));
                            }
                        }
                    }
                }

                encounteredPackages.UnionWith(remainingPackages);
                nextRound.ExceptWith(encounteredPackages);
                remainingPackages = nextRound;
            }

            foreach (KeyValuePair<string, string> package in componentRoots)
            {
                foreach (string path in Path.Combine(Paths.User.Content, package.Key, package.Value).EnumerateFiles($"{package.Key}.dll", SearchOption.AllDirectories))
                {
                    if (path.IndexOf($"{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) < 0
                        || (path.IndexOf($"{Path.DirectorySeparatorChar}netstandard1.", StringComparison.OrdinalIgnoreCase) < 0
                            && path.IndexOf($"{Path.DirectorySeparatorChar}netcoreapp1.", StringComparison.OrdinalIgnoreCase) < 0))
                    {
                        continue;
                    }

                    Assembly asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
                    foreach (Type type in asm.GetTypes())
                    {
                        SettingsLoader.Components.Register(type);
                    }
                }
            }

            foreach (string templateDir in templateRoots)
            {
                foreach (IMountPointFactory factory in SettingsLoader.Components.OfType<IMountPointFactory>())
                {
                    IMountPoint mountPoint;
                    if (factory.TryMount(null, templateDir, out mountPoint))
                    {
                        foreach (IGenerator generator in SettingsLoader.Components.OfType<IGenerator>())
                        {
                            foreach (ITemplate template in generator.GetTemplatesFromSource(mountPoint))
                            {
                                SettingsLoader.AddTemplate(template);
                                SettingsLoader.AddMountPoint(mountPoint);
                            }
                        }
                    }
                }
            }
        }

        private static void Init(ISettings settings)
        {
            Dictionary<string, PackageSource> sourceObjects = new Dictionary<string, PackageSource>(StringComparer.Ordinal);
            PackageSourceProvider packageSourceProvider = new PackageSourceProvider(settings);
            IEnumerable<PackageSource> packageSourcesFromProvider = packageSourceProvider.LoadPackageSources();

            // Use PackageSource objects from the provider when possible (since those will have credentials from nuget.config)
            foreach (PackageSource source in packageSourcesFromProvider)
            {
                if (source.IsEnabled && !sourceObjects.ContainsKey(source.Source))
                {
                    sourceObjects[source.Source] = source;
                }
            }

            // Create a shared caching provider if one does not exist already
            _cachingSourceProvider = _cachingSourceProvider ?? new CachingSourceProvider(packageSourceProvider);

            List<SourceRepository> repos = sourceObjects.Select(entry => _cachingSourceProvider.CreateRepository(entry.Value)).ToList();
            Repos.AddRange(repos);
        }

        private class Package
        {
            public Package(string packageId, VersionRange version)
            {
                PackageId = packageId.Trim();
                Version = version;
            }

            public string PackageId { get; }

            public VersionRange Version { get; }

            public override bool Equals(object obj)
            {
                Package other = obj as Package;

                if (other == null)
                {
                    return false;
                }

                return string.Equals(PackageId, other.PackageId, StringComparison.OrdinalIgnoreCase)
                       && (Version.IsSubSetOrEqualTo(other.Version) || other.Version.IsSubSetOrEqualTo(Version));
            }

            public override int GetHashCode() => PackageId.GetHashCode() ^ (Version?.GetHashCode() ?? 0);
        }
    }
}
