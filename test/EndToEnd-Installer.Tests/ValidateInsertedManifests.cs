// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace EndToEnd.Tests
{
    public class ValidateInsertedManifests : TestBase
    {
        [Fact]
        public void ManifestReaderCanReadManifests()
        {
            var sdkManifestDir = Path.Combine(Path.GetDirectoryName(RepoDirectoriesProvider.DotnetUnderTest), "sdk-manifests");
            var sdkversionDir = new DirectoryInfo(sdkManifestDir).EnumerateDirectories().First();
            foreach (var manifestVersionDir in sdkversionDir.EnumerateDirectories())
            {
                foreach (var manifestDir in manifestVersionDir.EnumerateDirectories())
                {
                    var manifestId = manifestVersionDir.Name;

                    string manifestFile = manifestDir.GetFile("WorkloadManifest.json").FullName;

                    if (!string.Equals(manifestId, "workloadsets"))
                    {
                        new FileInfo(manifestFile).Exists.Should().BeTrue();
                        using var fileStream = new FileStream(manifestFile, FileMode.Open, FileAccess.Read);
                        Action readManifest = () => WorkloadManifestReader.ReadWorkloadManifest(manifestId, fileStream, manifestFile);
                        readManifest.Should().NotThrow("manifestId:" + manifestId + " manifestFile:" + manifestFile + "is invalid");
                    }
                }
            }
            
        }
    }
}
