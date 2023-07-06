﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.NET.Sdk.WorkloadManifestReader;

using System;
using System.Collections.Generic;
using System.IO;

namespace ManifestReaderTests
{
    internal class MockManifestProvider : IWorkloadManifestProvider
    {
        readonly (string name, string path)[] _manifests;

        public MockManifestProvider(params string[] manifestPaths)
        {
            _manifests = Array.ConvertAll(manifestPaths, mp =>
            {
                string manifestId = Path.GetFileNameWithoutExtension(Path.GetDirectoryName(mp));
                return (manifestId, mp);
            });
            SdkFeatureBand = new SdkFeatureBand("6.0.100");
        }

        public SdkFeatureBand SdkFeatureBand { get; set; }

        public IEnumerable<string> GetManifestDirectories()
        {
            foreach ((_, var filePath) in _manifests)
            {
                yield return Path.GetDirectoryName(filePath);
            }
        }

        public IEnumerable<ReadableWorkloadManifest> GetManifests()
            {
                foreach ((var id, var path) in _manifests)
                {
                    yield return new(
                        id,
                        path,
                        () => File.OpenRead(path),
                        () => WorkloadManifestReader.TryOpenLocalizationCatalogForManifest(path)
                    );
                }
            }

        public string GetSdkFeatureBand() => SdkFeatureBand.ToString();
    }
}
