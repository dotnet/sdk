// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Client;
using NuGet.Common;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace Microsoft.DotNet.PackageValidation
{
    public class Package
    {
        private ManagedCodeConventions _conventions;
        private ContentItemCollection _packageAssets = new ContentItemCollection();
        private string _packagePath;
        private const string _nugetFeed = "https://api.nuget.org/v3/index.json";

        public Package(string packageId, string version, IEnumerable<string> packageAssets, Dictionary<NuGetFramework, IEnumerable<PackageDependency>> packageDependencies, string runtimeGraphPath, string packagePath)
            : this(packageId, version, packageAssets, packageDependencies, runtimeGraphPath)
        {

        }

        public Package(string packageId, string version, IEnumerable<string> packageAssets, Dictionary<NuGetFramework, IEnumerable<PackageDependency>> packageDependencies, string runtimeGraphPath, MemoryStream packageStream) 
            : this(packageId, version, packageAssets, packageDependencies, runtimeGraphPath)
        {
            PackageStream = packageStream;
        }

        public Package(string packageId, string version, IEnumerable<string> packageAssets, Dictionary<NuGetFramework, IEnumerable<PackageDependency>> packageDependencies, string runtimeGraphPath)
        {
            PackageId = packageId;
            Version = version;
            PackageDependencies = packageDependencies;
            _packageAssets.Load(packageAssets);

            RuntimeGraph runtimeGraph = null;
            if (!string.IsNullOrEmpty(runtimeGraphPath))
            {
                runtimeGraph = JsonRuntimeFormat.ReadRuntimeGraph(runtimeGraphPath);
            }
            _conventions = new ManagedCodeConventions(runtimeGraph);

            PackageAssets = _packageAssets.FindItems(_conventions.Patterns.AnyTargettedFile);
            RefAssets = _packageAssets.FindItems(_conventions.Patterns.CompileRefAssemblies);
            LibAssets = _packageAssets.FindItems(_conventions.Patterns.CompileLibAssemblies);
            CompileAssets = RefAssets.Any() ? RefAssets : LibAssets;

            RuntimeSpecificAssets = _packageAssets.FindItems(_conventions.Patterns.RuntimeAssemblies).Where(t => t.Path.StartsWith("runtimes"));
            RuntimeAssets = _packageAssets.FindItems(_conventions.Patterns.RuntimeAssemblies);

            Rids = RuntimeSpecificAssets?.Select(t => (string)t.Properties["rid"]);
            FrameworksInPackage = _packageAssets.FindItems(_conventions.Patterns.AnyTargettedFile)?.Select(t => (NuGetFramework)t.Properties["tfm"]);
        }

        public string PackageId { get; private set; }

        public string Version { get; private set; }

        public string PackagePath 
        {
            get
            {
                return _packagePath;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException($"{value} is empty or null.  Please check the package path.");
                }

                if (!File.Exists(Path.GetFullPath(value)))
                {
                    throw new FileNotFoundException($"{value} doesnt exist. Please check the package path.");
                }
                _packagePath = value;
            }
        }

        public MemoryStream PackageStream { get; set; }

        public Dictionary<NuGetFramework, IEnumerable<PackageDependency>> PackageDependencies { get; set; }

        public IEnumerable<ContentItem> CompileAssets { get; private set; }

        public IEnumerable<ContentItem> RefAssets { get; private set; }

        public IEnumerable<ContentItem> LibAssets { get; private set; }
        
        public IEnumerable<ContentItem> PackageAssets { get; private set; }

        public IEnumerable<ContentItem> RuntimeSpecificAssets { get; private set; }

        public IEnumerable<ContentItem> RuntimeAssets { get; private set; }

        public bool HasRefAssemblies => RefAssets.Any();

        public IEnumerable<string> Rids { get; private set; }

        public IEnumerable<NuGetFramework> FrameworksInPackage { get; private set; }

        public ContentItem FindBestRuntimeAssetForFramework(NuGetFramework framework)
        {
            SelectionCriteria managedCriteria = _conventions.Criteria.ForFramework(framework);
            return _packageAssets.FindBestItemGroup(managedCriteria,
                _conventions.Patterns.RuntimeAssemblies)?.Items.FirstOrDefault();
        }

        public ContentItem FindBestRuntimeAssetForFrameworkAndRuntime(NuGetFramework framework, string rid)
        {
            SelectionCriteria managedCriteria = _conventions.Criteria.ForFrameworkAndRuntime(framework, rid);
            return _packageAssets.FindBestItemGroup(managedCriteria,
                _conventions.Patterns.RuntimeAssemblies)?.Items.FirstOrDefault();
        }

        public ContentItem FindBestCompileAssetForFramework(NuGetFramework framework)
        {
            SelectionCriteria managedCriteria = _conventions.Criteria.ForFramework(framework);
            if (RefAssets.Any())
            {
                return _packageAssets.FindBestItemGroup(managedCriteria,
                    _conventions.Patterns.CompileRefAssemblies)?.Items.FirstOrDefault(); ;
            }
            else
            {
                return _packageAssets.FindBestItemGroup(managedCriteria,
                    _conventions.Patterns.CompileLibAssemblies)?.Items.FirstOrDefault(); ;
                
            }
        }

        private int VersionComparer(int major, int minor, int patch) => major * 100 + minor * 10 + patch;

        public async Task<MemoryStream> DownloadLatestStableVersionAsync(string nugetFeed = _nugetFeed)
        {
            SourceRepository repository = Repository.Factory.GetCoreV3(nugetFeed);
            FindPackageByIdResource resource = await repository.GetResourceAsync<FindPackageByIdResource>();
            SourceCacheContext cache = new SourceCacheContext();
            IEnumerable<NuGetVersion> versions = await resource.GetAllVersionsAsync(PackageId, cache, NullLogger.Instance, CancellationToken.None);

            if (versions == null)
            {
                throw new ArgumentException($"There is no version of the package in the feed {nugetFeed}");
            }

            NuGetVersion packageVersion = versions.Where(t => !t.IsPrerelease).OrderByDescending(t => VersionComparer(t.Major, t.Minor, t.Patch)).FirstOrDefault();
            
            if(packageVersion == null)
            {
                throw new ArgumentException($"There is no stable version of the package in the feed {nugetFeed}");
            }

            MemoryStream packageStream = new MemoryStream();

            await resource.CopyNupkgToStreamAsync(
                PackageId,
                packageVersion,
                packageStream,
                cache,
                NullLogger.Instance,
                CancellationToken.None);

            return packageStream;
        }
    }
}
