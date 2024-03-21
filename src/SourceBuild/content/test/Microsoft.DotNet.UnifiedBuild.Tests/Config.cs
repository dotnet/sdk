// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

public class Config : IDisposable
{
    IMessageSink _sink;
    public Config(IMessageSink sink)
    {
        _sink = sink;
        BuildVersion = Environment.GetEnvironmentVariable(BuildVersionEnv) ?? throw new InvalidOperationException($"'{BuildVersionEnv}' must be specified");
        PortableRid = Environment.GetEnvironmentVariable(PortableRidEnv) ?? throw new InvalidOperationException($"'{PortableRidEnv}' must be specified");
        UbSdkArchivePath = Environment.GetEnvironmentVariable(UbSdkTarballPathEnv) ?? throw new InvalidOperationException($"'{UbSdkTarballPathEnv}' must be specified");
        TargetRid = Environment.GetEnvironmentVariable(TargetRidEnv) ?? throw new InvalidOperationException($"'{TargetRidEnv}' must be specified");
        TargetArchitecture = TargetRid.Split('-')[1];
        WarnOnSdkContentDiffs = bool.TryParse(Environment.GetEnvironmentVariable(WarnSdkContentDiffsEnv), out bool warnOnSdkContentDiffs) && warnOnSdkContentDiffs;
        MsftSdkArchivePath = Environment.GetEnvironmentVariable(MsftSdkTarballPathEnv) ?? DownloadMsftSdkArchive().Result;
    }

    public const string BuildVersionEnv = "UNIFIED_BUILD_VALIDATION_BUILD_VERSION";
    public const string MsftSdkTarballPathEnv = "UNIFIED_BUILD_VALIDATION_MSFT_SDK_TARBALL_PATH";
    public const string PortableRidEnv = "UNIFIED_BUILD_VALIDATION_PORTABLE_RID";
    public const string PrereqsPathEnv = "UNIFIED_BUILD_VALIDATION_PREREQS_PATH";
    public const string UbSdkTarballPathEnv = "UNIFIED_BUILD_VALIDATION_SDK_TARBALL_PATH";
    public const string SourceBuiltArtifactsPathEnv = "UNIFIED_BUILD_VALIDATION_SOURCEBUILT_ARTIFACTS_PATH";
    public const string TargetRidEnv = "UNIFIED_BUILD_VALIDATION_TARGET_RID";
    public const string WarnSdkContentDiffsEnv = "UNIFIED_BUILD_VALIDATION_WARN_SDK_CONTENT_DIFFS";

    public string? MsftSdkArchivePath { get; }
    public string BuildVersion { get; }
    public string PortableRid { get; }
    public string UbSdkArchivePath { get; }
    public string TargetRid { get; }
    public string TargetArchitecture { get; }
    public bool WarnOnSdkContentDiffs { get; }
    string? _downloadedMsftSdkPath = null;

    static string GetArchiveExtension(string path)
    {
        if (path.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
            return ".zip";
        if (path.EndsWith(".tar.gz", StringComparison.InvariantCultureIgnoreCase))
            return ".tar.gz";
        throw new InvalidOperationException($"Path does not have a valid archive extenions: '{path}'");
    }

    public async Task<string> DownloadMsftSdkArchive()
    {
        var client = new HttpClient(new HttpClientHandler() { AllowAutoRedirect = false });
        var channel = BuildVersion[..5] + "xx";
        var akaMsUrl = $"https://aka.ms/dotnet/{channel}/daily/dotnet-sdk-{TargetRid}{GetArchiveExtension(UbSdkArchivePath)}";
        _sink.OnMessage(new DiagnosticMessage($"Downloading latest sdk from '{akaMsUrl}'"));
        var redirectResponse = await client.GetAsync(akaMsUrl);
        // aka.ms returns a 301 for valid redirects and a 302 to Bing for invalid URLs
        if (redirectResponse.StatusCode != HttpStatusCode.Moved)
        {
            throw new InvalidOperationException($"Could not find download link for Microsoft built sdk at '{akaMsUrl}'");
        }
        var closestUrl = redirectResponse.Headers.Location!.ToString();
        _sink.OnMessage(new DiagnosticMessage($"Redirected to '{closestUrl}'"));
        HttpResponseMessage packageResponse = await client.GetAsync(closestUrl);
        var packageUriPath = packageResponse.RequestMessage!.RequestUri!.LocalPath;
        _downloadedMsftSdkPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + "." + Path.GetFileName(packageUriPath));
        _sink.OnMessage(new DiagnosticMessage($"Downloading to '{_downloadedMsftSdkPath}'"));
        using (var file = File.Create(_downloadedMsftSdkPath))
        {
            await packageResponse.Content.CopyToAsync(file);
        }
        return _downloadedMsftSdkPath;
    }
    public void Dispose()
    {
        if (_downloadedMsftSdkPath != null)
        {
            File.Delete(_downloadedMsftSdkPath);
        }
    }
}
