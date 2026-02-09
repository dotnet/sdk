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

        [Fact(Skip = "No preview releases may be available after GA of a major release and before preview 1 of the next")]
        public void GetLatestVersionForChannel_Preview_ReturnsLatestPreviewVersion()
        {
            var manifest = new ChannelVersionResolver();
            var version = manifest.GetLatestVersionForChannel(new UpdateChannel("preview"), InstallComponent.SDK);

            Log.WriteLine($"Preview Version found: {version}");

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

        [Theory]
        [InlineData("latest", true)]
        [InlineData("preview", true)]
        [InlineData("lts", true)]
        [InlineData("sts", true)]
        [InlineData("LTS", true)]  // Case insensitive
        [InlineData("9", true)]
        [InlineData("9.0", true)]
        [InlineData("9.0.100", true)]
        [InlineData("9.0.1xx", true)]
        [InlineData("10", true)]
        [InlineData("99", true)]  // Max reasonable major
        [InlineData("99.0.100", true)]
        public void IsValidChannelFormat_ValidInputs_ReturnsTrue(string channel, bool expected)
        {
            Assert.Equal(expected, ChannelVersionResolver.IsValidChannelFormat(channel));
        }

        [Theory]
        [InlineData("939393939", false)]  // Way too large major version
        [InlineData("100", false)]  // Just over max reasonable
        [InlineData("999999", false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData("abc", false)]
        [InlineData("invalid", false)]
        [InlineData("-1", false)]  // Negative
        [InlineData("9.-1.100", false)]  // Negative minor
        public void IsValidChannelFormat_InvalidInputs_ReturnsFalse(string channel, bool expected)
        {
            Assert.Equal(expected, ChannelVersionResolver.IsValidChannelFormat(channel));
        }
        [Fact]
        public void GetSupportedChannels_WithFeatureBands_IncludesFeatureBandChannels()
        {
            var resolver = new ChannelVersionResolver();
            var channels = resolver.GetSupportedChannels(includeFeatureBands: true).ToList();

            // Should include named channels
            Assert.Contains("latest", channels);
            Assert.Contains("lts", channels);
            Assert.Contains("sts", channels);
            Assert.Contains("preview", channels);

            // Should include product versions like "10.0"
            Assert.Contains(channels, c => c.EndsWith(".0") && !c.Contains("xx"));

            // Should include feature bands like "10.0.1xx"
            Assert.Contains(channels, c => c.EndsWith("xx"));
        }

        [Fact]
        public void GetSupportedChannels_WithoutFeatureBands_ExcludesFeatureBandChannels()
        {
            var resolver = new ChannelVersionResolver();
            var channels = resolver.GetSupportedChannels(includeFeatureBands: false).ToList();

            // Should include named channels
            Assert.Contains("latest", channels);
            Assert.Contains("lts", channels);
            Assert.Contains("sts", channels);
            Assert.Contains("preview", channels);

            // Should include product versions like "10.0"
            Assert.Contains(channels, c => c.EndsWith(".0") && !c.Contains("xx"));

            // Should NOT include feature bands like "10.0.1xx"
            Assert.DoesNotContain(channels, c => c.EndsWith("xx"));
        }

        [Fact]
        public void GetSupportedChannels_DefaultIncludesFeatureBands()
        {
            var resolver = new ChannelVersionResolver();

            // Default call should include feature bands (for backward compatibility)
            var channels = resolver.GetSupportedChannels().ToList();

            Assert.Contains(channels, c => c.EndsWith("xx"));
        }

        #region UpdateChannel.IsSdkVersionOrFeatureBand Tests

        [Theory]
        [InlineData("9.0.1xx", true)]    // Feature band pattern
        [InlineData("9.0.2xx", true)]    // Feature band pattern
        [InlineData("10.0.1xx", true)]   // Feature band pattern
        [InlineData("9.0.12x", true)]    // Partial feature band pattern (unsupported but should be caught)
        [InlineData("9.0.103", true)]    // SDK version (patch >= 100)
        [InlineData("9.0.304", true)]    // SDK version (patch >= 100)
        [InlineData("10.0.100", true)]   // SDK version (patch >= 100)
        [InlineData("9.0.12", false)]    // Runtime version (patch < 100)
        [InlineData("9.0.0", false)]     // Runtime version (patch < 100)
        [InlineData("9.0.99", false)]    // Runtime version (patch < 100, edge case)
        [InlineData("latest", false)]    // Channel name
        [InlineData("lts", false)]       // Channel name
        [InlineData("9.0", false)]       // Major.minor channel
        [InlineData("9", false)]         // Major-only channel
        public void UpdateChannel_IsSdkVersionOrFeatureBand_DetectsCorrectly(string channel, bool expectedResult)
        {
            var updateChannel = new UpdateChannel(channel);
            Assert.Equal(expectedResult, updateChannel.IsSdkVersionOrFeatureBand());
        }

        #endregion
    }
}
