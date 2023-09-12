using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Xunit;
using Xunit.Abstractions;

namespace EndToEnd.Tests
{
    public class ValidateInsertedManifests : TestBase
    {
        private readonly ITestOutputHelper output;

        public ValidateInsertedManifests(ITestOutputHelper output)
        {
            this.output = output;
        }

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
                        readManifest.ShouldNotThrow("manifestId:" + manifestId + " manifestFile:" + manifestFile + "is invalid");
                    }
                }
            }
            
        }
    }
}
