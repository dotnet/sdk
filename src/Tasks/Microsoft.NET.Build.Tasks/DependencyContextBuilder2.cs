using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.DependencyModel;
using NuGet.Versioning;

namespace Microsoft.NET.Build.Tasks
{
    internal class DependencyContextBuilder2
    {
        private readonly SingleProjectInfo _mainProjectInfo;
        private readonly bool _includeRuntimeFileVersions;
        private IEnumerable<ReferenceInfo> _referenceAssemblies;
        private IEnumerable<ReferenceInfo> _directReferences;
        private Dictionary<string, List<RuntimePackAssetInfo>> _runtimePackAssets;
        private bool _includeMainProjectInDepsFile = true;
        private Dictionary<string, Dependency> _dependencyLookup;
        private List<DependencyLibrary> _dependencyLibraries;
        private Dictionary<string, List<LibraryDependency>> _libraryDependencies;
        private List<string> _mainProjectDependencies;
        private HashSet<string> _usedLibraryNames;
        private bool _isFrameworkDependent;
        private string _platformLibrary;

        private Dictionary<ReferenceInfo, string> _referenceLibraryNames;

        public DependencyContextBuilder2(SingleProjectInfo mainProjectInfo, ProjectContext projectContext, bool includeRuntimeFileVersions)
        {
            _mainProjectInfo = mainProjectInfo;
            _includeRuntimeFileVersions = includeRuntimeFileVersions;

            _dependencyLookup = projectContext.LockFileTarget.Libraries
                .Select(library => new Dependency(library.Name, library.Version.ToString()))
                .ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);

            _dependencyLibraries = projectContext.LockFileTarget.Libraries
                .Select(lockFileTargetLibrary => new DependencyLibrary()
                {
                    Name = lockFileTargetLibrary.Name,
                    Type = lockFileTargetLibrary.Type
                }).ToList();

            _libraryDependencies = new Dictionary<string, List<LibraryDependency>>(StringComparer.OrdinalIgnoreCase);
            foreach (var library in projectContext.LockFileTarget.Libraries)
            {
                _libraryDependencies[library.Name] = library.Dependencies
                    .Select(d => new LibraryDependency()
                    {
                        Name = d.Id,
                        MinVersion = d.VersionRange.MinVersion
                    }).ToList();
            }

            _mainProjectDependencies = projectContext.GetTopLevelDependencies().ToList();

            _usedLibraryNames = new HashSet<string>(_dependencyLookup.Keys, StringComparer.OrdinalIgnoreCase);

            _isFrameworkDependent = projectContext.IsFrameworkDependent;
            _platformLibrary = projectContext.PlatformLibrary?.Name;
        }

        private Dictionary<ReferenceInfo, string> ReferenceLibraryNames
        {
            get
            {
                if (_referenceLibraryNames == null)
                {
                    _referenceLibraryNames = new Dictionary<ReferenceInfo, string>();
                }

                return _referenceLibraryNames;
            }
        }

        public DependencyContextBuilder2 WithReferenceAssemblies(IEnumerable<ReferenceInfo> referenceAssemblies)
        {
            // note: ReferenceAssembly libraries only export compile-time stuff
            // since they assume the runtime library is present already
            _referenceAssemblies = referenceAssemblies;
            return this;
        }

        public DependencyContextBuilder2 WithDirectReferences(IEnumerable<ReferenceInfo> directReferences)
        {
            _directReferences = directReferences;
            return this;
        }

        public DependencyContextBuilder2 WithMainProjectInDepsFile(bool includeMainProjectInDepsFile)
        {
            _includeMainProjectInDepsFile = includeMainProjectInDepsFile;
            return this;
        }

        public DependencyContextBuilder2 WithRuntimePackAssets(IEnumerable<RuntimePackAssetInfo> runtimePackAssets)
        {
            _runtimePackAssets = new Dictionary<string, List<RuntimePackAssetInfo>>();
            foreach (var runtimePackGroup in runtimePackAssets.GroupBy(a => a.PackageName))
            {
                var dependency = new Dependency(runtimePackGroup.Key, runtimePackGroup.First().PackageVersion);
                _dependencyLookup.Add(dependency.Name, dependency);

                _runtimePackAssets[dependency.Name] = runtimePackGroup.ToList();
            }
            return this;
        }

        public DependencyContext Build()
        {
            List<RuntimeLibrary> runtimeLibraries = new List<RuntimeLibrary>();

            if (_includeMainProjectInDepsFile)
            {
                runtimeLibraries.Add(GetProjectRuntimeLibrary());
            }

            runtimeLibraries.AddRange(GetRuntimePackLibraries());

            throw new NotImplementedException();
        }

        private RuntimeLibrary GetProjectRuntimeLibrary()
        {
            RuntimeAssetGroup[] runtimeAssemblyGroups = new[] { new RuntimeAssetGroup(string.Empty, _mainProjectInfo.OutputName) };

            List<Dependency> dependencies = new List<Dependency>();
            foreach (var dependencyName in _mainProjectDependencies)
            {
                if (_dependencyLookup.TryGetValue(dependencyName, out Dependency dependency))
                {
                    dependencies.Add(dependency);
                }
            }

            if (_directReferences != null)
            {
                foreach (var directReference in _directReferences)
                {
                    dependencies.Add(
                        new Dependency(
                            GetReferenceLibraryName(directReference),
                            directReference.Version));
                }
            }
            if (_runtimePackAssets != null)
            {
                foreach (var runtimePackName in _runtimePackAssets.Keys)
                {
                    dependencies.Add(_dependencyLookup[runtimePackName]);
                }
            }

            return new RuntimeLibrary(
                type: "project",
                name: _mainProjectInfo.Name,
                version: _mainProjectInfo.Version,
                hash: string.Empty,
                runtimeAssemblyGroups: runtimeAssemblyGroups,
                nativeLibraryGroups: Array.Empty<RuntimeAssetGroup>(),
                resourceAssemblies: CreateResourceAssemblies(_mainProjectInfo.ResourceAssemblies),
                dependencies: dependencies,
                path: null,
                hashPath: null,
                runtimeStoreManifestName: GetRuntimeStoreManifestName(_mainProjectInfo.Name, _mainProjectInfo.Version),
                serviceable: false);
        }

        private IEnumerable<RuntimeLibrary> GetRuntimePackLibraries()
        {
            if (_runtimePackAssets == null)
            {
                return Enumerable.Empty<RuntimeLibrary>();
            }
            return _runtimePackAssets.Select(runtimePack =>
            {
                var runtimeAssemblyGroup = new RuntimeAssetGroup(string.Empty,
                    runtimePack.Value.Where(asset => asset.AssetType == AssetType.Runtime)
                    .Select(asset => CreateRuntimeFile(asset.DestinationSubPath, asset.SourcePath)));

                var nativeLibraryGroup = new RuntimeAssetGroup(string.Empty,
                    runtimePack.Value.Where(asset => asset.AssetType == AssetType.Native)
                    .Select(asset => CreateRuntimeFile(asset.DestinationSubPath, asset.SourcePath)));

                return new RuntimeLibrary(
                    type: "runtimepack",
                    name: runtimePack.Key,
                    version: runtimePack.Value.First().PackageVersion,
                    hash: string.Empty,
                    runtimeAssemblyGroups: new[] { runtimeAssemblyGroup },
                    nativeLibraryGroups: new[] { nativeLibraryGroup },
                    resourceAssemblies: Enumerable.Empty<ResourceAssembly>(),
                    dependencies: Enumerable.Empty<Dependency>(),
                    serviceable: false);
            });
        }

        private RuntimeFile CreateRuntimeFile(string path, string fullPath)
        {
            if (_includeRuntimeFileVersions)
            {
                string fileVersion = FileUtilities.GetFileVersion(fullPath).ToString();
                string assemblyVersion = FileUtilities.TryGetAssemblyVersion(fullPath)?.ToString();
                return new RuntimeFile(path, assemblyVersion, fileVersion);
            }
            else
            {
                return new RuntimeFile(path, null, null);
            }
        }

        private static IEnumerable<ResourceAssembly> CreateResourceAssemblies(IEnumerable<ResourceAssemblyInfo> resourceAssemblyInfos)
        {
            return resourceAssemblyInfos
                .Select(r => new ResourceAssembly(r.RelativePath, r.Culture));
        }

        private IEnumerable<DependencyLibrary> GetFilteredLibraries()
        {
            var libraries = _dependencyLibraries;

            HashSet<string> allExclusionList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (_isFrameworkDependent && !string.IsNullOrEmpty(_platformLibrary))
            {
                allExclusionList.Add(_platformLibrary);

                Stack<LibraryDependency> dependenciesToWalk = new Stack<LibraryDependency>(_libraryDependencies[_platformLibrary]);

                while (dependenciesToWalk.Any())
                {

                }
                
            }

            throw new NotImplementedException();
        }

        private string GetReferenceLibraryName(ReferenceInfo reference)
        {
            if (!ReferenceLibraryNames.TryGetValue(reference, out string name))
            {
                // Reference names can conflict with PackageReference names, so
                // ensure that the Reference names are unique when creating libraries
                name = GetUniqueReferenceName(reference.Name);

                ReferenceLibraryNames.Add(reference, name);
                _usedLibraryNames.Add(name);
            }

            return name;
        }

        private string GetUniqueReferenceName(string name)
        {
            if (_usedLibraryNames.Contains(name))
            {
                string startingName = $"{name}.Reference";
                name = startingName;

                int suffix = 1;
                while (_usedLibraryNames.Contains(name))
                {
                    name = $"{startingName}{suffix++}";
                }
            }

            return name;
        }

        private string GetRuntimeStoreManifestName(string packageName, string packageVersion)
        {
            throw new NotImplementedException();
        }

        private struct DependencyLibrary
        {
            public string Name { get; set; }
            public string Type { get; set; }
        }

        private struct LibraryDependency
        {
            public string Name { get; set; }
            public NuGetVersion MinVersion { get; set; }
        }
    }
}
