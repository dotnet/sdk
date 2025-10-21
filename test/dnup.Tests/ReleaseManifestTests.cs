using System;
using Xunit;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.Dotnet.Installation;

namespace Microsoft.DotNet.Tools.Dnup.Tests
{
    public class ReleaseManifestTests
    {
        [Fact]
        public void GetLatestVersionForChannel_MajorOnly_ReturnsLatestVersion()
        {
            var manifest = new ReleaseManifest();
            var version = manifest.GetLatestVersionForChannel(new UpdateChannel("9"), InstallComponent.SDK);
            Assert.NotNull(version);
        }

        [Fact]
        public void GetLatestVersionForChannel_MajorMinor_ReturnsLatestVersion()
        {
            var manifest = new ReleaseManifest();
            var version = manifest.GetLatestVersionForChannel(new UpdateChannel("9"), InstallComponent.SDK);
            Assert.NotNull(version);
            Assert.StartsWith("9.0.", version.ToString());
        }

        [Fact]
        public void GetLatestVersionForChannel_FeatureBand_ReturnsLatestVersion()
        {
            var manifest = new ReleaseManifest();

            var version = manifest.GetLatestVersionForChannel(new UpdateChannel("9.0.1xx"), InstallComponent.SDK);
            Console.WriteLine($"Version found: {version}");

            // Feature band version should be returned in the format 9.0.100
            Assert.NotNull(version);
            Assert.Matches(@"^9\.0\.1\d{2}$", version.ToString());
        }

        [Fact]
        public void GetLatestVersionForChannel_LTS_ReturnsLatestLTSVersion()
        {
            var manifest = new ReleaseManifest();
            var version = manifest.GetLatestVersionForChannel(new UpdateChannel("lts"), InstallComponent.SDK);

            Console.WriteLine($"LTS Version found: {version}");

            // Check that we got a version
            Assert.NotNull(version);

            // LTS versions should have even major versions (e.g., 6.0, 8.0, 10.0)
            Assert.True(version.Minor % 2 == 0, $"LTS version {version} should have an even minor version");

            // Should not be a preview version
            Assert.Null(version.Prerelease);
        }

        [Fact]
        public void GetLatestVersionForChannel_STS_ReturnsLatestSTSVersion()
        {
            var manifest = new ReleaseManifest();
            var version = manifest.GetLatestVersionForChannel(new UpdateChannel("sts"), InstallComponent.SDK);

            Console.WriteLine($"STS Version found: {version}");

            // Check that we got a version
            Assert.NotNull(version);

            // STS versions should have odd major versions (e.g., 7.0, 9.0, 11.0)
            Assert.True(version.Major % 2 != 0, $"STS version {version} should have an odd minor version");

            // Should not be a preview version
            Assert.Null(version.Prerelease);
        }

        [Fact]
        public void GetLatestVersionForChannel_Preview_ReturnsLatestPreviewVersion()
        {
            var manifest = new ReleaseManifest();
            var version = manifest.GetLatestVersionForChannel(new UpdateChannel("preview"), InstallComponent.SDK);

            Console.WriteLine($"Preview Version found: {version}");

            // Check that we got a version
            Assert.NotNull(version);

            // Preview versions should contain a hyphen (e.g., "11.0.0-preview.1")
            Assert.NotNull(version.Prerelease);

            // Should contain preview, rc, beta, or alpha
            Assert.True(
                version.Prerelease.Contains("preview", StringComparison.OrdinalIgnoreCase) ||
                version.Prerelease.Contains("rc", StringComparison.OrdinalIgnoreCase) ||
                version.Prerelease.Contains("beta", StringComparison.OrdinalIgnoreCase) ||
                version.Prerelease.Contains("alpha", StringComparison.OrdinalIgnoreCase),
                $"Version {version} should be a preview/rc/beta/alpha version"
            );
        }
    }
}
