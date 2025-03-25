// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Arcade.Common;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.Manifest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.UnifiedBuild.Tasks
{
    public class MergeAssetManifests : MSBuildTaskBase
    {
        /// <summary>
        /// AssetManifest paths
        /// </summary>
        [Required]
        public required ITaskItem[] AssetManifest { get; init; }

        /// <summary>
        /// Merged asset manifest output path
        /// </summary>
        [Required]
        public required string MergedAssetManifestOutputPath { get; init; }

        /// <summary>
        /// Vmr Vertical Name, e.g. "Android_Shortstack_arm". Allowed to be empty for non official builds.
        /// </summary>
        public string VerticalName { get; set; } = string.Empty;

        private static readonly string _verticalNameAttribute = "VerticalName";

        private IBuildModelFactory? _buildModelFactory;
        private IFileSystem? _fileSystem;

        public override void ConfigureServices(IServiceCollection collection)
        {
            collection.TryAddSingleton<IBuildModelFactory, BuildModelFactory>();
            collection.TryAddSingleton<IBlobArtifactModelFactory, BlobArtifactModelFactory>();
            collection.TryAddSingleton<IPdbArtifactModelFactory, PdbArtifactModelFactory>();
            collection.TryAddSingleton<IPackageArtifactModelFactory, PackageArtifactModelFactory>();
            collection.TryAddSingleton<INupkgInfoFactory, NupkgInfoFactory>();
            collection.TryAddSingleton<IPackageArchiveReaderFactory, PackageArchiveReaderFactory>();
            collection.TryAddSingleton<IFileSystem, FileSystem>();
            collection.TryAddSingleton(Log);
        }

        public bool ExecuteTask(IBuildModelFactory buildModelFactory,
                                IFileSystem fileSystem)
        {
            _buildModelFactory = buildModelFactory;
            _fileSystem = fileSystem;

            MergeManifests();
            return !Log.HasLoggedErrors;
        }

        public void MergeManifests()
        {
            if (_buildModelFactory == null)
            {
                throw new InvalidOperationException("BuildModelFactory is not initialized.");
            }

            if (_fileSystem == null)
            {
                throw new InvalidOperationException("FileSystem is not initialized.");
            }

            var assetManifestModels = AssetManifest.Select(xmlPath => _buildModelFactory.ManifestFileToModel(xmlPath.ItemSpec))
                .ToList();

            // We may encounter assets here with "Vertical", "Internal", or "External" visibility here.
            // We filter out "Vertical" visibility assets here, as they are not needed in the merged manifest.
            // We leave in "Internal" assets so they can be used in later build passes.
            // We leave in "External" assets as we will eventually ship them.

            var mergedManifest = _buildModelFactory.CreateMergedModel(assetManifestModels, ArtifactVisibility.Internal | ArtifactVisibility.External);

            // Add a vertical name in the merged model's build identity
            mergedManifest.Identity.Attributes.Add(_verticalNameAttribute, VerticalName);

            _fileSystem.CreateDirectory(_fileSystem.GetDirectoryName(MergedAssetManifestOutputPath)!);
            _fileSystem.WriteToFile(MergedAssetManifestOutputPath, mergedManifest.ToXml().ToString());

            Log.LogMessage(MessageImportance.High, $"Merged asset manifest written to {MergedAssetManifestOutputPath}");
        }
    }
}
