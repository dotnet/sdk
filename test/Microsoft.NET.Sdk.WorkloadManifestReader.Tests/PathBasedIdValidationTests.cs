// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace ManifestReaderTests
{
    public class PathBasedIdValidationTests
    {
        [Theory]
        [InlineData("../escape")]
        [InlineData(".")]
        [InlineData("..")]
        [InlineData("")]
        [InlineData(" ")]
        public void PathBasedIdsRejectInvalidComponents(string id)
        {
            Assert.Throws<ArgumentException>(() => new ManifestId(id));
            Assert.Throws<ArgumentException>(() => new WorkloadId(id));
            Assert.Throws<ArgumentException>(() => new WorkloadPackId(id));
        }

        [Fact]
        public void PathBasedIdsRejectNull()
        {
            Assert.Throws<ArgumentException>(() => new ManifestId(null!));
            Assert.Throws<ArgumentException>(() => new WorkloadId(null!));
            Assert.Throws<ArgumentException>(() => new WorkloadPackId(null!));
        }

        [WindowsOnlyTheory]
        [InlineData(@"..\escape")]
        [InlineData(@"C:\escape")]
        [InlineData("C:relative")]
        [InlineData("invalid:name")]
        [InlineData("trailingdot.")]
        [InlineData("trailingspace ")]
        public void PathBasedIdsRejectWindowsInvalidComponents(string id)
        {
            Assert.Throws<ArgumentException>(() => new ManifestId(id));
            Assert.Throws<ArgumentException>(() => new WorkloadId(id));
            Assert.Throws<ArgumentException>(() => new WorkloadPackId(id));
        }

        [Fact]
        public void InvalidWorkloadIdInManifestThrowsManifestFormatException()
        {
            const string manifest = "{\n" +
                "    \"version\": \"1.0.0\",\n" +
                "    \"workloads\": {\n" +
                "        \"../escape\": {\n" +
                "            \"description\": \"Invalid workload\"\n" +
                "        }\n" +
                "    }\n" +
                "}";
            using MemoryStream manifestStream = new(Encoding.UTF8.GetBytes(manifest));

            var exception = Assert.Throws<WorkloadManifestFormatException>(
                () => WorkloadManifestReader.ReadWorkloadManifest("Invalid", manifestStream, "invalid.json"));

            Assert.IsType<ArgumentException>(exception.InnerException);
            Assert.Contains("valid path component", exception.Message);
            Assert.Contains("at offset", exception.Message);
        }
    }
}
