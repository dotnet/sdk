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

namespace Microsoft.DotNet.Tools.Dotnetup.Tests
{
    [TestClass]
    public class ChannelVersionResolverTests(TestContext testContext)
    {
        ITestOutputHelper Log = new TestContextOutputHelper(testContext);

        [TestMethod]
        public void GetLatestVersionForChannel_MajorOnly_ReturnsLatestVersion()
        {
            var manifest = new ChannelVersionResolver();
            var version = manifest.GetLatestVersionForChannel(new UpdateChannel("9"), InstallComponent.SDK);
            Assert.IsNotNull(version);
        }

        [TestMethod]
        public void GetLatestVersionForChannel_MajorMinor_ReturnsLatestVersion()
        {
            var manifest = new ChannelVersionResolver();
            var version = manifest.GetLatestVersionForChannel(new UpdateChannel("9.0"), InstallComponent.SDK);
            Assert.IsNotNull(version);
            Assert.StartsWith("9.0.", version.ToString());
        }

        [TestMethod]
        public void GetLatestVersionForChannel_FeatureBand_ReturnsLatestVersion()
        {
            var manifest = new ChannelVersionResolver();

            var version = manifest.GetLatestVersionForChannel(new UpdateChannel("9.0.1xx"), InstallComponent.SDK);
            Log.WriteLine($"Version found: {version}");

            // Feature band version should be returned in the format 9.0.100
            Assert.IsNotNull(version);
            Assert.MatchesRegex(@"^9\.0\.1\d{2}$", version.ToString());
        }

        [TestMethod]
        public void GetLatestVersionForChannel_LTS_ReturnsLatestLTSVersion()
        {
            var manifest = new ChannelVersionResolver();
            var version = manifest.GetLatestVersionForChannel(new UpdateChannel("lts"), InstallComponent.SDK);

            Log.WriteLine($"LTS Version found: {version}");

            // Check that we got a version
            Assert.IsNotNull(version);

            // LTS versions should have even major versions (e.g., 6.0, 8.0, 10.0)
            Assert.AreEqual(0, version.Major % 2, $"LTS version {version} should have an even major version");

            // Should not be a preview version
            Assert.IsNull(version.Prerelease);
        }

        [TestMethod]
        public void GetLatestVersionForChannel_STS_IsNoLongerSupported()
        {
            var manifest = new ChannelVersionResolver();

            // "sts" channel has been removed - it should not resolve to a version
            var version = manifest.GetLatestVersionForChannel(new UpdateChannel("sts"), InstallComponent.SDK);
            Assert.IsNull(version);
        }

        [TestMethod]
        [Ignore("No preview releases may be available after GA of a major release and before preview 1 of the next")]
        public void GetLatestVersionForChannel_Preview_ReturnsLatestPreviewVersion()
        {
            var manifest = new ChannelVersionResolver();
            var version = manifest.GetLatestVersionForChannel(new UpdateChannel("preview"), InstallComponent.SDK);

            Log.WriteLine($"Preview Version found: {version}");

            // Check that we got a version
            Assert.IsNotNull(version);

            // Preview versions should contain a hyphen (e.g., "11.0.0-preview.1")
            Assert.IsNotNull(version.Prerelease);

            // Should contain preview, rc, beta, or alpha
            Assert.IsTrue(
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
        [TestMethod]
        [DataRow("daily")]
        [DataRow("10.0-daily")]
        [DataRow("11.0-daily")]
        [DataRow("11.0.1xx-preview.5-daily")]
        [DataRow("11.0.1xx-preview5-daily")]
        [DataRow("11.0-preview.5-daily")]
        public void GetLatestVersionForChannel_Daily_ResolvesAgainstLiveAkaMs(string channelName)
        {
            var resolver = new ChannelVersionResolver();
            var version = resolver.GetLatestVersionForChannel(new UpdateChannel(channelName), InstallComponent.SDK);

            Log.WriteLine($"Daily channel '{channelName}' resolved to: {version}");

            Assert.IsNotNull(version);
            Assert.IsFalse(string.IsNullOrEmpty(version.Prerelease), $"Daily-channel version {version} should have a prerelease tag");
        }

        [TestMethod]
        [DataRow("10.0.100-preview.1.32640")]
        [DataRow("11.0.100-preview.3.26170.106")]
        [DataRow("9.0.103-rc.1.24123.4")]
        public void GetLatestVersionForChannel_FullyQualifiedPrereleaseVersion_ReturnsExactVersion(string channel)
        {
            var resolver = new ChannelVersionResolver();
            var version = resolver.GetLatestVersionForChannel(new UpdateChannel(channel), InstallComponent.SDK);
            Assert.IsNotNull(version);
            Assert.AreEqual(channel, version!.ToString());
        }

        [TestMethod]
        [DataRow("preview", true)]
        [DataRow("lts", true)]
        [DataRow("LTS", true)]  // Case insensitive
        [DataRow("9", true)]
        [DataRow("9.0", true)]
        [DataRow("9.0.100", true)]
        [DataRow("9.0.1xx", true)]
        [DataRow("10", true)]
        [DataRow("99", true)]  // Max reasonable major
        [DataRow("99.0.100", true)]
        [DataRow("10.0.100-preview.1.32640", true)]  // Full prerelease version
        [DataRow("daily", true)]
        [DataRow("DAILY", true)]
        [DataRow("10-daily", true)]
        [DataRow("10.0-daily", true)]
        [DataRow("10.0.1xx-daily", true)]
        [DataRow("11.0.1xx-preview.5-daily", true)]
        [DataRow("11.0.1xx-preview5-daily", true)]
        [DataRow("10.0.1xx-rc.1-daily", true)]
        [DataRow("10.0-DAILY", true)]
        public void IsValidChannelFormat_ValidInputs_ReturnsTrue(string channel, bool expected)
        {
            Assert.AreEqual(expected, ChannelVersionResolver.IsValidChannelFormat(channel));
        }

        [TestMethod]
        [DataRow("939393939", false)]  // Way too large major version
        [DataRow("100", false)]  // Just over max reasonable
        [DataRow("999999", false)]
        [DataRow("", false)]
        [DataRow("   ", false)]
        [DataRow("abc", false)]
        [DataRow("invalid", false)]
        [DataRow("-1", false)]  // Negative
        [DataRow("9.-1.100", false)]  // Negative minor
        [DataRow("10.0.1xxx", false)]  // Invalid wildcard
        [DataRow("10.0.1xx-preview.1", false)]  // Wildcards with prerelease not supported
        [DataRow("10.0.103-daily", false)]  // Daily applies only to scopes, not specific patches
        [DataRow("-daily", false)]  // Empty scope before -daily
        [DataRow("preview-daily", false)]  // Named channels can't take -daily
        [DataRow("100-daily", false)]  // Major outside reasonable range
        [DataRow("11.0.1xx--daily", false)]  // Empty phase label
        [DataRow("11.0.1xx-preview-daily", false)]  // Phase label missing number
        [DataRow("11.0.1xx-5-daily", false)]  // Phase label missing letters
        [DataRow("11.0.103-preview.5-daily", false)]  // Specific patch + phase still rejected
        [DataRow("invalid.invalid.1xx-daily", false)]  // Non-numeric scope
        [DataRow("bad.0.1xx-preview.5-daily", false)]  // Valid phase label but invalid band still rejected
        public void IsValidChannelFormat_InvalidInputs_ReturnsFalse(string channel, bool expected)
        {
            Assert.AreEqual(expected, ChannelVersionResolver.IsValidChannelFormat(channel));
        }
        [TestMethod]
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
            Assert.Contains(c => c.EndsWith(".0") && !c.Contains("xx"), channels);

            // Should include feature bands like "10.0.1xx"
            Assert.Contains(c => c.EndsWith("xx"), channels);
        }

        [TestMethod]
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
            Assert.Contains(c => c.EndsWith(".0") && !c.Contains("xx"), channels);

            // Should NOT include feature bands like "10.0.1xx"
            Assert.DoesNotContain(c => c.EndsWith("xx"), channels);
        }

        [TestMethod]
        public void GetSupportedChannels_DefaultIncludesFeatureBands()
        {
            var resolver = new ChannelVersionResolver();

            // Default call should include feature bands (for backward compatibility)
            var channels = resolver.GetSupportedChannels().ToList();

            Assert.Contains(c => c.EndsWith("xx"), channels);
        }

        [TestMethod]
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

            Assert.IsNotNull(version);
            Assert.AreEqual("10.0.100-preview.4.25216.37", version!.ToString());
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

        [TestMethod]
        [DataRow("9.0.1xx", true)]    // Feature band pattern
        [DataRow("9.0.2xx", true)]    // Feature band pattern
        [DataRow("10.0.1xx", true)]   // Feature band pattern
        [DataRow("9.0.12x", true)]    // Partial feature band pattern (unsupported but should be caught)
        [DataRow("9.0.103", true)]    // SDK version (patch >= 100)
        [DataRow("9.0.304", true)]    // SDK version (patch >= 100)
        [DataRow("10.0.100", true)]   // SDK version (patch >= 100)
        [DataRow("9.0.12", false)]    // Runtime version (patch < 100)
        [DataRow("9.0.0", false)]     // Runtime version (patch < 100)
        [DataRow("9.0.99", false)]    // Runtime version (patch < 100, edge case)
        [DataRow("latest", false)]    // Channel name
        [DataRow("lts", false)]       // Channel name
        [DataRow("9.0", false)]       // Major.minor channel
        [DataRow("9", false)]         // Major-only channel
        public void UpdateChannel_IsSdkVersionOrFeatureBand_DetectsCorrectly(string channel, bool expectedResult)
        {
            var updateChannel = new UpdateChannel(channel);
            Assert.AreEqual(expectedResult, updateChannel.IsSdkVersionOrFeatureBand());
        }

        #endregion
    }
}
