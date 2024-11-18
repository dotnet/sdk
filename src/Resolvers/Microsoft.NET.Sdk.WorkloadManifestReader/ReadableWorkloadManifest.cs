// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable IDE0240
#nullable enable
#pragma warning restore IDE0240

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public class ReadableWorkloadManifest
    {
        public string ManifestId { get; }

        public string ManifestDirectory { get; }

        public string ManifestPath { get; }

        public string ManifestFeatureBand { get; }

        public string ManifestVersion { get; }

        readonly Func<Stream> _openManifestStreamFunc;


        readonly Func<Stream?> _openLocalizationStream;

        public ReadableWorkloadManifest(string manifestId, string manifestDirectory, string manifestPath, string manifestFeatureBand, string manifestVersion, Func<Stream> openManifestStreamFunc, Func<Stream?> openLocalizationStream)
        {
            ManifestId = manifestId;
            ManifestPath = manifestPath;
            ManifestDirectory = manifestDirectory;
            ManifestFeatureBand = manifestFeatureBand;
            ManifestVersion = manifestVersion;
            _openManifestStreamFunc = openManifestStreamFunc;
            _openLocalizationStream = openLocalizationStream;
        }

        public Stream OpenManifestStream()
        {
            return _openManifestStreamFunc();
        }

        public Stream? OpenLocalizationStream()
        {
            return _openLocalizationStream();
        }

    }
}
