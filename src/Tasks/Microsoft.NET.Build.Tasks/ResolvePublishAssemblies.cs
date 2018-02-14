﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Versioning;
using System.Collections.Generic;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Resolves the assemblies to be published for a .NET app.
    /// </summary>
    public class ResolvePublishAssemblies : TaskBase
    {
        private readonly List<ITaskItem> _assembliesToPublish = new List<ITaskItem>();

        [Required]
        public string ProjectPath { get; set; }

        [Required]
        public string AssetsFilePath { get; set; }

        [Required]
        public string TargetFramework { get; set; }

        public string RuntimeIdentifier { get; set; }

        public string PlatformLibraryName { get; set; }

        public ITaskItem[] ExcludeFromPublishPackageReferences { get; set; }

        public bool PreserveStoreLayout { get; set; }

        public ITaskItem[] RuntimeStorePackages { get; set; }

        public bool IsSelfContained { get; set; }

        /// <summary>
        /// All the assemblies to publish.
        /// </summary>
        [Output]
        public ITaskItem[] AssembliesToPublish
        {
            get { return _assembliesToPublish.ToArray(); }
        }

        protected override void ExecuteCore()
        {
            var lockFileCache = new LockFileCache(BuildEngine4);
            LockFile lockFile = lockFileCache.GetLockFile(AssetsFilePath);
            IEnumerable<string> excludeFromPublishPackageIds = PackageReferenceConverter.GetPackageIds(ExcludeFromPublishPackageReferences);
            IPackageResolver packageResolver = NuGetPackageResolver.CreateResolver(lockFile, ProjectPath);
            HashSet<PackageIdentity> packagestoBeFiltered = null;

            if (RuntimeStorePackages != null && RuntimeStorePackages.Length > 0)
            {
                packagestoBeFiltered = new HashSet<PackageIdentity>();
                foreach (var package in RuntimeStorePackages)
                {
                    packagestoBeFiltered.Add(ItemUtilities.GetPackageIdentity(package));
                }
            }

            ProjectContext projectContext = lockFile.CreateProjectContext(
                NuGetUtils.ParseFrameworkName(TargetFramework),
                RuntimeIdentifier,
                PlatformLibraryName,
                IsSelfContained);

            projectContext.PackagesToBeFiltered = packagestoBeFiltered;

            var assemblyResolver =
                new PublishAssembliesResolver(packageResolver)
                    .WithExcludeFromPublish(excludeFromPublishPackageIds)
                    .WithPreserveStoreLayout(PreserveStoreLayout);

            IEnumerable<ResolvedFile> resolvedAssemblies = assemblyResolver.Resolve(projectContext);
            foreach (ResolvedFile resolvedAssembly in resolvedAssemblies)
            {
                TaskItem item = new TaskItem(resolvedAssembly.SourcePath);
                item.SetMetadata("DestinationSubPath", resolvedAssembly.DestinationSubPath);
                item.SetMetadata("AssetType", resolvedAssembly.Asset.ToString().ToLower());
                item.SetMetadata(MetadataKeys.PackageName, resolvedAssembly.Package.Id.ToString().ToLower());
                item.SetMetadata(MetadataKeys.PackageVersion, resolvedAssembly.Package.Version.ToString().ToLower());
                _assembliesToPublish.Add(item);
            }

        }
    }
}
