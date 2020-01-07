﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.DependencyModel;
using Newtonsoft.Json;
using NuGet.Packaging.Core;
using NuGet.RuntimeModel;
using NuGet.ProjectModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Generates the $(project).deps.json file.
    /// </summary>
    public class GenerateDepsFile : TaskBase
    {
        [Required]
        public string ProjectPath { get; set; }

        public string AssetsFilePath { get; set; }

        [Required]
        public string DepsFilePath { get; set; }

        [Required]
        public string TargetFramework { get; set; }

        public string RuntimeIdentifier { get; set; }

        public string PlatformLibraryName { get; set; }

        public ITaskItem[] RuntimeFrameworks { get; set; }

        [Required]
        public string AssemblyName { get; set; }

        [Required]
        public string AssemblyExtension { get; set; }

        [Required]
        public string AssemblyVersion { get; set; }

        public ITaskItem[] AssemblySatelliteAssemblies { get; set; } = Array.Empty<ITaskItem>();

        [Required]
        public bool IncludeMainProject { get; set; }

        // @(ReferencePath) that will be passed to
        public ITaskItem[] ReferencePaths { get; set; } = Array.Empty<ITaskItem>();

        // Full set of @(ReferenceDependencyPaths) found by RAR
        public ITaskItem[] ReferenceDependencyPaths { get; set; } = Array.Empty<ITaskItem>();

        // Full set of @(ReferenceSatellitePaths) found by RAR
        public ITaskItem[] ReferenceSatellitePaths { get; set; } = Array.Empty<ITaskItem>();

        // Subset of @(ReferencePath) that is not CopyLocal, used for compilation, but not runtime assets
        public ITaskItem[] ReferenceAssemblies { get; set; } = Array.Empty<ITaskItem>();

        // Runtime assets for self-contained deployment from runtime pack
        public ITaskItem[] RuntimePackAssets { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem CompilerOptions { get; set; }

        public ITaskItem[] RuntimeStorePackages { get; set; }

        // NuGet compilation assets
        [Required]
        public ITaskItem[] CompileReferences { get; set; }

        // NuGet runtime assets for root directory: @(NativeCopyLocalItems), @(ResourceCopyLocalItems), @(RuntimeCopyLocalItems)
        [Required]
        public ITaskItem[] ResolvedNuGetFiles { get; set; }

        // NuGet runtime assets for runtimes* directory
        [Required]
        public ITaskItem[] ResolvedRuntimeTargetsFiles { get; set; }

        // CopyLocal subset ot of @(ReferencePath), @(ReferenceDependencyPath)
        // Used to filter out non-runtime assemblies from deps file. Only project and direct references in this
        // set will be written to deps file as runtime dependencies.
        public string[] UserRuntimeAssemblies { get; set; }

        public bool IsSelfContained { get; set; }

        public bool IncludeRuntimeFileVersions { get; set; }

        [Required]
        public string RuntimeGraphPath { get; set; }

        List<ITaskItem> _filesWritten = new List<ITaskItem>();

        [Output]
        public ITaskItem[] FilesWritten
        {
            get { return _filesWritten.ToArray(); }
        }

        private Dictionary<PackageIdentity, string> GetFilteredPackages()
        {
            Dictionary<PackageIdentity, string> filteredPackages = null;

            if (RuntimeStorePackages != null && RuntimeStorePackages.Length > 0)
            {
                filteredPackages = new Dictionary<PackageIdentity, string>();
                foreach (var package in RuntimeStorePackages)
                {
                    filteredPackages.Add(
                        ItemUtilities.GetPackageIdentity(package),
                        package.GetMetadata(MetadataKeys.RuntimeStoreManifestNames));
                }
            }

            return filteredPackages;
        }

        private void WriteDepsFile(string depsFilePath)
        {
            ProjectContext projectContext;
            if (AssetsFilePath == null)
            {
                projectContext = null;
            }
            else
            {
                LockFile lockFile = new LockFileCache(this).GetLockFile(AssetsFilePath);
                projectContext = lockFile.CreateProjectContext(
                 NuGetUtils.ParseFrameworkName(TargetFramework),
                 RuntimeIdentifier,
                 PlatformLibraryName,
                 RuntimeFrameworks,
                 IsSelfContained);
            }

            CompilationOptions compilationOptions = CompilationOptionsConverter.ConvertFrom(CompilerOptions);

            SingleProjectInfo mainProject = SingleProjectInfo.Create(
                ProjectPath,
                AssemblyName,
                AssemblyExtension,
                AssemblyVersion,
                AssemblySatelliteAssemblies);

            var userRuntimeAssemblySet = new HashSet<string>(UserRuntimeAssemblies ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            Func<ITaskItem, bool> isUserRuntimeAssembly = item => userRuntimeAssemblySet.Contains(item.ItemSpec);

            IEnumerable<ReferenceInfo> referenceAssemblyInfos =
                ReferenceInfo.CreateReferenceInfos(ReferenceAssemblies);

            // If there is a generated asset file. The projectContext will have project reference.
            // So remove it from directReferences to avoid duplication
            var projectContextHasProjectReferences = projectContext != null;
            IEnumerable<ReferenceInfo> directReferences =
                ReferenceInfo.CreateDirectReferenceInfos(ReferencePaths,
                    ReferenceSatellitePaths,
                    projectContextHasProjectReferences, isUserRuntimeAssembly);

            IEnumerable<ReferenceInfo> dependencyReferences =
                ReferenceInfo.CreateDependencyReferenceInfos(ReferenceDependencyPaths, ReferenceSatellitePaths, isUserRuntimeAssembly);

            Dictionary<string, SingleProjectInfo> referenceProjects =
                SingleProjectInfo.CreateProjectReferenceInfos(ReferencePaths, ReferenceSatellitePaths, isUserRuntimeAssembly);

            IEnumerable<RuntimePackAssetInfo> runtimePackAssets =
                IsSelfContained ? RuntimePackAssets.Select(item => RuntimePackAssetInfo.FromItem(item)) : Enumerable.Empty<RuntimePackAssetInfo>();

            DependencyContextBuilder builder;
            if (projectContext != null)
            {
                // Generate the RID-fallback for self-contained builds.
                //
                // In order to support loading components with RID-specific assets, 
                // the AssemblyDependencyResolver requires a RID fallback graph.
                // The component itself should not carry the RID fallback graph with it, because
                // it would need to carry graph of all the RIDs and needs updates for newer RIDs.
                // For framework dependent apps, the RID fallback graph comes from the core framework Microsoft.NETCore.App, 
                // so there is no need to write it into the app.
                // If self-contained apps, the (applicable subset of) RID fallback graph needs to be written to the deps.json manifest.
                //
                // If a RID-graph is provided to the DependencyContextBuilder, it generates a RID-fallback 
                // graph with respect to the target RuntimeIdentifier.

                RuntimeGraph runtimeGraph =
                    IsSelfContained ? new RuntimeGraphCache(this).GetRuntimeGraph(RuntimeGraphPath) : null;

                builder = new DependencyContextBuilder(mainProject, IncludeRuntimeFileVersions, runtimeGraph, projectContext);
            }
            else
            {
                builder = new DependencyContextBuilder(
                    mainProject,
                    IncludeRuntimeFileVersions,
                    RuntimeFrameworks,
                    isSelfContained: IsSelfContained,
                    platformLibraryName: PlatformLibraryName,
                    runtimeIdentifier: RuntimeIdentifier,
                    targetFramework: TargetFramework);
            }

            builder = builder
                .WithMainProjectInDepsFile(IncludeMainProject)
                .WithReferenceAssemblies(referenceAssemblyInfos)
                .WithDirectReferences(directReferences)
                .WithDependencyReferences(dependencyReferences)
                .WithReferenceProjectInfos(referenceProjects)
                .WithRuntimePackAssets(runtimePackAssets)
                .WithCompilationOptions(compilationOptions)
                .WithReferenceAssembliesPath(FrameworkReferenceResolver.GetDefaultReferenceAssembliesPath())
                .WithPackagesThatWereFiltered(GetFilteredPackages());

            if (CompileReferences.Length > 0)
            {
                builder = builder.WithCompileReferences(ReferenceInfo.CreateReferenceInfos(CompileReferences));
            }

            var resolvedNuGetFiles = ResolvedNuGetFiles.Select(f => new ResolvedFile(f, false))
                                .Concat(ResolvedRuntimeTargetsFiles.Select(f => new ResolvedFile(f, true)));
            builder = builder.WithResolvedNuGetFiles(resolvedNuGetFiles);

            DependencyContext dependencyContext = builder.Build();

            var writer = new DependencyContextWriter();
            using (var fileStream = File.Create(depsFilePath))
            {
                writer.Write(dependencyContext, fileStream);
            }
            _filesWritten.Add(new TaskItem(depsFilePath));
        }

        protected override void ExecuteCore()
        {
            WriteDepsFile(DepsFilePath);
        }
    }
}
