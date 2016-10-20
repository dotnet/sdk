﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.ProjectModel;

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

        public ITaskItem[] PrivateAssetsPackageReferences { get; set; }

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
            LockFile lockFile = new LockFileCache(BuildEngine4).GetLockFile(AssetsFilePath);
            NuGetPathContext nugetPathContext = NuGetPathContext.Create(Path.GetDirectoryName(ProjectPath));
            IEnumerable<string> privateAssetsPackageIds = PackageReferenceConverter.GetPackageIds(PrivateAssetsPackageReferences);

            ProjectContext projectContext = lockFile.CreateProjectContext(
                NuGetUtils.ParseFrameworkName(TargetFramework),
                RuntimeIdentifier);

            IEnumerable<ResolvedFile> resolvedAssemblies = 
                new PublishAssembliesResolver(new NuGetPackageResolver(nugetPathContext))
                    .WithPrivateAssets(privateAssetsPackageIds)
                    .Resolve(projectContext);

            foreach (ResolvedFile resolvedAssembly in resolvedAssemblies)
            {
                TaskItem item = new TaskItem(resolvedAssembly.SourcePath);
                item.SetMetadata("DestinationSubPath", resolvedAssembly.DestinationSubPath);

                _assembliesToPublish.Add(item);
            }
        }
    }
}
