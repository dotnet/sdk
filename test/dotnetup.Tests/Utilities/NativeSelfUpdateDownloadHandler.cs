// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http;
using System.Security.Cryptography;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

internal sealed class NativeSelfUpdateDownloadHandler : HttpMessageHandler
{
    private readonly byte[] _replacementBytes;
    private readonly List<Uri> _requests = [];

    public NativeSelfUpdateDownloadHandler(NativeSelfUpdateFiles files)
    {
        string version = files.Release.Version.ToString();
        ArtifactUri = new Uri($"https://ci.dot.net/public/dotnetup/{version}/dotnetup-win-x64.exe");
        ChecksumUri = new Uri($"https://ci.dot.net/public-checksums/dotnetup/{version}/dotnetup-win-x64.exe.sha512");
        BuildIdUri = new Uri(ArtifactUri.AbsoluteUri + ".buildid");
        DailyFinalUri = ArtifactUri;
        _replacementBytes = File.ReadAllBytes(files.ReplacementPath);
        PublishedHash = Convert.ToHexString(SHA512.HashData(_replacementBytes));
        PublishedBuildId = files.ReplacementIdentity;
    }

    public static Uri DailyUri { get; } = new("https://aka.ms/dotnet/dotnetup/daily/dotnetup-win-x64.exe");
    public Uri ArtifactUri { get; }
    public Uri ChecksumUri { get; }
    public Uri BuildIdUri { get; }
    public Uri DailyFinalUri { get; set; }
    public string PublishedHash { get; set; }
    public string PublishedBuildId { get; set; }
    public IReadOnlyList<Uri> Requests => _requests;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Assert.AreEqual(HttpMethod.Get, request.Method);
        Assert.IsNotNull(request.RequestUri);
        Assert.AreEqual(Uri.UriSchemeHttps, request.RequestUri.Scheme);
        _requests.Add(request.RequestUri);

        HttpContent content;
        Uri finalUri = request.RequestUri;
        if (request.RequestUri == DailyUri)
        {
            finalUri = DailyFinalUri;
            content = new ByteArrayContent([]);
        }
        else if (request.RequestUri == ChecksumUri)
        {
            content = new StringContent($"{PublishedHash}  dotnetup-win-x64.exe\n");
        }
        else if (request.RequestUri == BuildIdUri)
        {
            content = new StringContent(PublishedBuildId + "\r\n");
        }
        else if (request.RequestUri == ArtifactUri)
        {
            content = new ByteArrayContent(_replacementBytes);
        }
        else
        {
            throw new AssertFailedException($"Unexpected self-update request: {request.RequestUri}");
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, finalUri),
            Content = content,
        });
    }
}