// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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
            var version = manifest.GetLatestVersionForChannel(new UpdateChannel("9.0"), InstallComponent.SDK);
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
            Assert.True(version.Major % 2 == 0, $"LTS version {version} should have an even major version");

            // Should not be a preview version
            Assert.Null(version.Prerelease);
        }

        [Fact]
        public void GetLatestVersionForChannel_STS_IsNoLongerSupported()
        {
            var manifest = new ChannelVersionResolver();

            // "sts" channel has been removed - it should not resolve to a version
            var version = manifest.GetLatestVersionForChannel(new UpdateChannel("sts"), InstallComponent.SDK);
            Assert.Null(version);
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

        /// <summary>
        /// Daily-channel resolution against the live aka.ms infrastructure. Probes the
        /// three forms a user might type. Bare <c>daily</c> tries major+1 first and falls
        /// back to the manifest's highest major; channel-scoped forms (<c>10.0-daily</c>,
        /// <c>11.0-daily</c>) target a specific major.minor. If aka.ms isn't redirecting
        /// for one of these majors (e.g. a fresh major hasn't started producing dailies),
        /// the version comes back null and the test fails — that's the signal we want.
        /// </summary>
        [Theory]
        [InlineData("daily")]
        [InlineData("10.0-daily")]
        [InlineData("11.0-daily")]
        public void GetLatestVersionForChannel_Daily_ResolvesAgainstLiveAkaMs(string channelName)
        {
            var resolver = new ChannelVersionResolver();
            var version = resolver.GetLatestVersionForChannel(new UpdateChannel(channelName), InstallComponent.SDK);

            Log.WriteLine($"Daily channel '{channelName}' resolved to: {version}");

            Assert.NotNull(version);
            Assert.False(string.IsNullOrEmpty(version.Prerelease), $"Daily-channel version {version} should have a prerelease tag");
        }

        [Theory]
        [InlineData("10.0.100-preview.1.32640")]
        [InlineData("11.0.100-preview.3.26170.106")]
        [InlineData("9.0.103-rc.1.24123.4")]
        public void GetLatestVersionForChannel_FullyQualifiedPrereleaseVersion_ReturnsExactVersion(string channel)
        {
            var resolver = new ChannelVersionResolver();
            var version = resolver.GetLatestVersionForChannel(new UpdateChannel(channel), InstallComponent.SDK);
            Assert.NotNull(version);
            Assert.Equal(channel, version!.ToString());
        }

        [Theory]
        [InlineData("preview", true)]
        [InlineData("lts", true)]
        [InlineData("LTS", true)]  // Case insensitive
        [InlineData("9", true)]
        [InlineData("9.0", true)]
        [InlineData("9.0.100", true)]
        [InlineData("9.0.1xx", true)]
        [InlineData("10", true)]
        [InlineData("99", true)]  // Max reasonable major
        [InlineData("99.0.100", true)]
        [InlineData("10.0.100-preview.1.32640", true)]  // Full prerelease version
        [InlineData("daily", true)]
        [InlineData("DAILY", true)]
        [InlineData("10-daily", true)]
        [InlineData("10.0-daily", true)]
        [InlineData("10.0.1xx-daily", true)]
        [InlineData("10.0-DAILY", true)]
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
        [InlineData("10.0.1xxx", false)]  // Invalid wildcard
        [InlineData("10.0.1xx-preview.1", false)]  // Wildcards with prerelease not supported
        [InlineData("10.0.103-daily", false)]  // Daily applies only to scopes, not specific patches
        [InlineData("-daily", false)]  // Empty scope before -daily
        [InlineData("preview-daily", false)]  // Named channels can't take -daily
        [InlineData("100-daily", false)]  // Major outside reasonable range
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
            Assert.Contains("preview", channels);

            // "sts" channel has been removed
            Assert.DoesNotContain("sts", channels);

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
            Assert.Contains("preview", channels);

            // "sts" channel has been removed
            Assert.DoesNotContain("sts", channels);

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

        [Fact]
        public void GetLatestVersionForChannel_DailyChannel_RoutesThroughDailyChannelResolver()
        {
            // Wiring test: GetLatestVersionForChannel for a daily channel must invoke
            // DailyChannelResolver, not the release-manifest path. We prove this by stubbing
            // the aka.ms redirect target — if the daily resolver isn't called, the returned
            // version won't match the redirect URL we set up.
            const string redirectTarget =
                "https://ci.dot.net/public/Sdk/10.0.100-preview.4.25216.37/dotnet-sdk-10.0.100-preview.4.25216.37-win-x64.zip";

            using var handler = new StubRedirectHandler(new Dictionary<string, string>
            {
                ["https://aka.ms/dotnet/10.0.1xx/daily/dotnet-sdk-"] = redirectTarget,
            });
            using var httpClient = new HttpClient(handler);
            using var dailyResolver = new DailyChannelResolver(new ReleaseManifest(), httpClient);

            var resolver = new ChannelVersionResolver(new ReleaseManifest(), dailyResolver);

            var version = resolver.GetLatestVersionForChannel(new UpdateChannel("10.0.1xx-daily"), InstallComponent.SDK, InstallArchitecture.x64);

            Assert.NotNull(version);
            Assert.Equal("10.0.100-preview.4.25216.37", version!.ToString());
        }

        /// <summary>
        /// Maps URL prefixes to redirect target URLs, emulating the post-redirect state of
        /// HttpClient after aka.ms redirects to the actual blob storage location.
        /// </summary>
        private sealed class StubRedirectHandler : HttpMessageHandler
        {
            private readonly Dictionary<string, string> _redirectMap;

            public StubRedirectHandler(Dictionary<string, string> redirectMap)
            {
                _redirectMap = redirectMap;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string url = request.RequestUri!.ToString();

                foreach (var (prefix, target) in _redirectMap)
                {
                    if (url.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(string.Empty),
                            RequestMessage = new HttpRequestMessage(HttpMethod.Get, target),
                        });
                    }
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request });
            }
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
