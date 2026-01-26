using System;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests
{
    public class ChannelVersionResolverTests(ITestOutputHelper log)
    {
        ITestOutputHelper Log = log;

        [Fact]
        public void GetLatestVersionForChannel_MajorOnly_ReturnsLatestVersion()
        {
            var manifest = new ChannelVersionResolver();
            var version = manifest.GetLatestVersionForChannel(new UpdateChannel("9"), InstallComponent.SDK);
            Assert.NotNull(version);
        }

        [Fact]
        public void GetLatestVersionForChannel_MajorMinor_ReturnsLatestVersion()
        {
            var manifest = new ChannelVersionResolver();
            var version = manifest.GetLatestVersionForChannel(new UpdateChannel("9"), InstallComponent.SDK);
            Assert.NotNull(version);
            Assert.StartsWith("9.0.", version.ToString());
        }

        [Fact]
        public void GetLatestVersionForChannel_FeatureBand_ReturnsLatestVersion()
        {
            var manifest = new ChannelVersionResolver();

            var version = manifest.GetLatestVersionForChannel(new UpdateChannel("9.0.1xx"), InstallComponent.SDK);
            Log.WriteLine($"Version found: {version}");

            // Feature band version should be returned in the format 9.0.100
            Assert.NotNull(version);
            Assert.Matches(@"^9\.0\.1\d{2}$", version.ToString());
        }

        [Fact]
        public void GetLatestVersionForChannel_LTS_ReturnsLatestLTSVersion()
        {
            var manifest = new ChannelVersionResolver();
            var version = manifest.GetLatestVersionForChannel(new UpdateChannel("lts"), InstallComponent.SDK);

            Log.WriteLine($"LTS Version found: {version}");

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
            var manifest = new ChannelVersionResolver();
            var version = manifest.GetLatestVersionForChannel(new UpdateChannel("sts"), InstallComponent.SDK);

            Log.WriteLine($"STS Version found: {version}");

            // Check that we got a version
            Assert.NotNull(version);

            // STS versions should have odd major versions (e.g., 7.0, 9.0, 11.0)
            Assert.True(version.Major % 2 != 0, $"STS version {version} should have an odd minor version");

            // Should not be a preview version
            Assert.Null(version.Prerelease);
        }

        [Fact]
        public void GetLatestVersionForChannel_Preview_ReturnsLatestPreviewOrFallsBackToGA()
        {
            var manifest = new ChannelVersionResolver();
            var version = manifest.GetLatestVersionForChannel(new UpdateChannel("preview"), InstallComponent.SDK);

            Log.WriteLine($"Preview Version found: {version}");

            // Check that we got a version
            Assert.NotNull(version);

            // Version could be either a preview/rc/beta/alpha OR a GA version (fallback)
            // If it's a preview, it should have Prerelease set
            if (version.Prerelease != null)
            {
                // Should contain preview, rc, beta, or alpha
                Assert.True(
                    version.Prerelease.Contains("preview", StringComparison.OrdinalIgnoreCase) ||
                    version.Prerelease.Contains("rc", StringComparison.OrdinalIgnoreCase) ||
                    version.Prerelease.Contains("beta", StringComparison.OrdinalIgnoreCase) ||
                    version.Prerelease.Contains("alpha", StringComparison.OrdinalIgnoreCase),
                    $"Version {version} should be a preview/rc/beta/alpha version"
                );
            }
            else
            {
                // If no preview is available, we should get the latest GA version as fallback
                Log.WriteLine($"No preview available, fell back to GA version: {version}");
            }
        }

        [Fact]
        public void GetLatestVersionForChannel_PreviewWithNoFallback_ReturnsNullWhenNoPreview()
        {
            var manifest = new ChannelVersionResolver();
            var version = manifest.GetLatestVersionForChannel(new UpdateChannel("preview"), InstallComponent.SDK, noFallback: true);

            Log.WriteLine($"Preview Version found (with noFallback): {version}");

            // With noFallback set, we should either get a preview version or null
            if (version != null)
            {
                // If we got a version, it must be a preview (not GA)
                Assert.NotNull(version.Prerelease);
                Assert.True(
                    version.Prerelease.Contains("preview", StringComparison.OrdinalIgnoreCase) ||
                    version.Prerelease.Contains("rc", StringComparison.OrdinalIgnoreCase) ||
                    version.Prerelease.Contains("beta", StringComparison.OrdinalIgnoreCase) ||
                    version.Prerelease.Contains("alpha", StringComparison.OrdinalIgnoreCase),
                    $"Version {version} should be a preview/rc/beta/alpha version when noFallback is true"
                );
            }
            else
            {
                // null is acceptable when noFallback is true and no preview exists
                Log.WriteLine("No preview available and noFallback is true, returned null as expected");
            }
        }
    }
}
