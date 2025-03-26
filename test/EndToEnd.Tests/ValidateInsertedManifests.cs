// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace EndToEnd.Tests
{
    public class ValidateInsertedManifests(ITestOutputHelper log) : SdkTest(log)
    {
        [Fact]
        public void ManifestReaderCanReadManifests()
        {
            var sdkManifestDir = Path.Combine(Path.GetDirectoryName(TestContext.Current.ToolsetUnderTest.DotNetHostPath), "sdk-manifests");
            var sdkversionDir = new DirectoryInfo(sdkManifestDir).EnumerateDirectories().First();
            foreach (var manifestVersionDir in sdkversionDir.EnumerateDirectories())
            {
                foreach (var manifestDir in manifestVersionDir.EnumerateDirectories())
                {
                    var manifestId = manifestVersionDir.Name;

                    string manifestFile = new FileInfo(Path.Combine(manifestDir.FullName, "WorkloadManifest.json")).FullName;

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
