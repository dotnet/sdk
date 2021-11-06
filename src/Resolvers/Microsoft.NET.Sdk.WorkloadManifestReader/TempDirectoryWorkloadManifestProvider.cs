﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public class TempDirectoryWorkloadManifestProvider : IWorkloadManifestProvider
    {
        private readonly string _manifestsPath;
        private readonly string _sdkVersionBand;

        /// <param name="manifestsPath">
        ///     Result of directly extract manifest NuGet Packages. Should contain folders like
        ///     microsoft.net.workload.emscripten.manifest-6.0.100.6.0.0-preview.7.21377.2
        /// </param>
        public TempDirectoryWorkloadManifestProvider(string manifestsPath, string sdkVersion)
        {
            _manifestsPath = manifestsPath;
            _sdkVersionBand = sdkVersion;
        }

        public IEnumerable<(string manifestId, string? informationalPath, Func<Stream> openManifestStream, Func<Stream?> openLocalizationStream)>
            GetManifests()
        {
            foreach (var workloadManifestDirectory in GetManifestDirectories())
            {
                string? workloadManifestPath = Path.Combine(workloadManifestDirectory, "WorkloadManifest.json");
                var manifestId = Path.GetFileName(workloadManifestDirectory);

                int index = manifestId.IndexOf(".Manifest", StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    manifestId = manifestId.Substring(0, index);
                }

                yield return (
                    manifestId,
                    workloadManifestPath,
                    () => File.OpenRead(workloadManifestPath),
                    () => WorkloadManifestReader.TryOpenLocalizationCatalogForManifest(workloadManifestPath)
                );
            }
        }

        public IEnumerable<string> GetManifestDirectories()
        {
            if (Directory.Exists(_manifestsPath))
            {
                foreach (var workloadManifestDirectory in Directory.EnumerateDirectories(_manifestsPath))
                {
                    yield return workloadManifestDirectory;
                }
            }
        }

        public string GetSdkFeatureBand() => _sdkVersionBand;
    }
}
