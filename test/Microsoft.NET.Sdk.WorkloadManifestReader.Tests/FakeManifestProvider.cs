﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace ManifestReaderTests
{
    internal class FakeManifestProvider : IWorkloadManifestProvider
    {
        readonly (string manifest, string? localizationCatalog)[] _filePaths;

        public FakeManifestProvider(params string[] filePaths)
        {
            _filePaths = filePaths.Select(p => (p, (string?)null)).ToArray();
        }

        public FakeManifestProvider(params (string manifest, string? localizationCatalog)[] filePaths)
        {
            _filePaths = filePaths;
        }

        public void RefreshWorkloadManifests() { }

        public IEnumerable<ReadableWorkloadManifest> GetManifests()
        {
            foreach (var filePath in _filePaths)
            {
                yield return new(
                    Path.GetFileNameWithoutExtension(filePath.manifest),
                    Path.GetDirectoryName(filePath.manifest)!,
                    filePath.manifest,
                    "8.0.100",
                    "33",
                    () => new FileStream(filePath.manifest, FileMode.Open, FileAccess.Read),
                    () => filePath.localizationCatalog != null ? new FileStream(filePath.localizationCatalog, FileMode.Open, FileAccess.Read) : null
                );
            }
        }

        public string GetSdkFeatureBand() => "8.0.100";
        public Dictionary<string, WorkloadSet> GetAvailableWorkloadSets() => throw new NotImplementedException();
        public string? GetWorkloadVersion() => "8.0.100.2";
    }

    internal class InMemoryFakeManifestProvider : IWorkloadManifestProvider, IEnumerable<(string id, string content)>
    {
        readonly List<(string id, byte[] content)> _manifests = new();

        public void Add(string id, string content) => _manifests.Add((id, Encoding.UTF8.GetBytes(content)));

        public void RefreshWorkloadManifests() { }

        public IEnumerable<ReadableWorkloadManifest> GetManifests()
            => _manifests.Select(m => new ReadableWorkloadManifest(
                m.id,
                $@"C:\fake\{m.id}",
                $@"C:\fake\{m.id}\WorkloadManifest.json",
                "8.0.100",
                "34",
                (Func<Stream>)(() => new MemoryStream(m.content)),
                (Func<Stream?>)(() => null)
            ));

        // these are just so the collection initializer works
        public IEnumerator<(string id, string content)> GetEnumerator() => throw new NotImplementedException();
        IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
        public string GetSdkFeatureBand() => "8.0.100";
        public Dictionary<string, WorkloadSet> GetAvailableWorkloadSets() => throw new NotImplementedException();
        public string? GetWorkloadVersion() => "8.0.100.2";
    }
}
