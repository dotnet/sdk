// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class DailyChannelResolverTests
{
    private const string SampleArchiveUrl =
        "https://ci.dot.net/public/Sdk/10.0.100-preview.4.25216.37/dotnet-sdk-10.0.100-preview.4.25216.37-win-x64.zip";

    [Fact]
    public void Resolve_ScopedDaily_ExtractsVersionFromRedirectTarget()
    {
        using var handler = new RedirectHandler(new Dictionary<string, string>
        {
            // Match any aka.ms request that starts with the scope path.
            ["https://aka.ms/dotnet/10.0/daily/dotnet-sdk-"] = SampleArchiveUrl,
        });
        using var httpClient = new HttpClient(handler);
        using var resolver = new DailyChannelResolver(new ReleaseManifest(), httpClient);

        var version = resolver.Resolve(new UpdateChannel("10.0-daily"), InstallArchitecture.x64);

        version.Should().NotBeNull();
        version!.ToString().Should().Be("10.0.100-preview.4.25216.37");
    }

    [Fact]
    public void Resolve_BareMajorDaily_NormalizesToMajorMinor()
    {
        using var handler = new RedirectHandler(new Dictionary<string, string>
        {
            ["https://aka.ms/dotnet/10.0/daily/dotnet-sdk-"] = SampleArchiveUrl,
        });
        using var httpClient = new HttpClient(handler);
        using var resolver = new DailyChannelResolver(new ReleaseManifest(), httpClient);

        var version = resolver.Resolve(new UpdateChannel("10-daily"), InstallArchitecture.x64);

        version.Should().NotBeNull();
        version!.ToString().Should().Be("10.0.100-preview.4.25216.37");
    }

    [Fact]
    public void Resolve_FeatureBandDaily_PassesScopeThrough()
    {
        using var handler = new RedirectHandler(new Dictionary<string, string>
        {
            ["https://aka.ms/dotnet/10.0.1xx/daily/dotnet-sdk-"] = SampleArchiveUrl,
        });
        using var httpClient = new HttpClient(handler);
        using var resolver = new DailyChannelResolver(new ReleaseManifest(), httpClient);

        var version = resolver.Resolve(new UpdateChannel("10.0.1xx-daily"), InstallArchitecture.x64);

        version.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_AkaMsReturnsNotFound_ReturnsNull()
    {
        // Empty redirect map → handler returns 404 for every request.
        using var handler = new RedirectHandler(new Dictionary<string, string>());
        using var httpClient = new HttpClient(handler);
        using var resolver = new DailyChannelResolver(new ReleaseManifest(), httpClient);

        var version = resolver.Resolve(new UpdateChannel("10.0-daily"), InstallArchitecture.x64);

        version.Should().BeNull();
    }

    [Fact]
    public void Resolve_NonDailyChannel_Throws()
    {
        using var resolver = new DailyChannelResolver();

        Assert.Throws<ArgumentException>(() =>
            resolver.Resolve(new UpdateChannel("10.0"), InstallArchitecture.x64));
    }

    [Theory]
    [InlineData("https://ci.dot.net/public/Sdk/10.0.100-preview.4.25216.37/dotnet-sdk-10.0.100-preview.4.25216.37-win-x64.zip", true)]
    [InlineData("https://builds.dotnet.microsoft.com/sdk/10.0.100-preview.4.25216.37/dotnet-sdk.zip", true)]
    [InlineData("https://dotnetbuilds.azureedge.net/public/Sdk/10.0.100/dotnet-sdk.zip", true)]
    [InlineData("https://evil.example.com/Sdk/10.0.100-preview.4/dotnet-sdk.zip", false)]
    [InlineData("http://ci.dot.net/public/Sdk/10.0.100/dotnet-sdk.zip", false)] // HTTP rejected
    public void IsAllowedRedirectTarget_RecognizesAllowlist(string url, bool expected)
    {
        DailyChannelResolver.IsAllowedRedirectTarget(new Uri(url)).Should().Be(expected);
    }

    [Theory]
    // Real aka.ms not-found redirect shape (observed against https://aka.ms/dotnet/12.0/daily/...):
    [InlineData("https://www.bing.com/?ref=aka&shorturl=dotnet/12.0/daily/dotnet-sdk-win-x64.zip", true)]
    [InlineData("https://bing.com/?ref=aka&shorturl=dotnet/12.0/daily/dotnet-sdk-linux-x64.tar.gz", true)]
    // Case-insensitive host and query.
    [InlineData("https://WWW.BING.COM/?REF=AKA&shorturl=dotnet/12.0/daily/dotnet-sdk-osx-arm64.tar.gz", true)]
    // Plain bing.com without the ref=aka marker — could be a real user-facing redirect chain; don't treat as not-found.
    [InlineData("https://www.bing.com/search?q=dotnet+sdk", false)]
    [InlineData("https://www.bing.com/", false)]
    // Legitimate daily-build hosts: never match the not-found pattern.
    [InlineData("https://ci.dot.net/public/Sdk/10.0.100/dotnet-sdk.zip", false)]
    [InlineData("https://builds.dotnet.microsoft.com/sdk/10.0.100/dotnet-sdk.zip", false)]
    public void IsAkaMsShortlinkNotFound_RecognizesBingFallbackPattern(string url, bool expected)
    {
        DailyChannelResolver.IsAkaMsShortlinkNotFound(new Uri(url)).Should().Be(expected);
    }

    [Theory]
    [InlineData("https://ci.dot.net/public/Sdk/10.0.100-preview.4.25216.37/dotnet-sdk-10.0.100-preview.4.25216.37-win-x64.zip", "10.0.100-preview.4.25216.37")]
    [InlineData("https://builds.dotnet.microsoft.com/sdk/9.0.103/dotnet-sdk-9.0.103-win-x64.zip", "9.0.103")]
    [InlineData("https://ci.dot.net/no-version-here/file.zip", null)]
    public void ExtractVersionFromUrl_FindsFirstParseableSegment(string url, string? expected)
    {
        var version = DailyChannelResolver.ExtractVersionFromUrl(new Uri(url));

        if (expected == null)
        {
            version.Should().BeNull();
        }
        else
        {
            version.Should().NotBeNull();
            version!.ToString().Should().Be(expected);
        }
    }

    /// <summary>
    /// Stub HTTP handler that maps a URL prefix to a final URL. When a request
    /// comes in matching a prefix, it returns a 200 response whose
    /// <see cref="HttpResponseMessage.RequestMessage"/> URI is rewritten to
    /// the final URL — emulating the effect of HttpClient following 30x
    /// redirects all the way to the blob storage URL.
    /// </summary>
    private sealed class RedirectHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _redirectMap;

        public RedirectHandler(Dictionary<string, string> redirectMap)
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
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(string.Empty),
                        // Rewrite the URI on the request to simulate the post-redirect state.
                        RequestMessage = new HttpRequestMessage(HttpMethod.Get, target),
                    };
                    return Task.FromResult(response);
                }
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                RequestMessage = request,
            });
        }
    }
}
