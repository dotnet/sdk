using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            foreach (var manifestDir in sdkversionDir.EnumerateDirectories())
            {
                var manifestId = manifestDir.Name;

                string manifestFile = manifestDir.GetFile("WorkloadManifest.json").FullName;

                File.Exists(manifestFile).Should().BeTrue();
                using var fileStream = new FileStream(manifestFile, FileMode.Open, FileAccess.Read);
                Action readManifest = () => WorkloadManifestReader.ReadWorkloadManifest(manifestId, fileStream, manifestFile);
                readManifest.ShouldNotThrow("manifestId:" + manifestId + " manifestFile:" + manifestFile + "is invalid");
            }
            
        }
    }
}
