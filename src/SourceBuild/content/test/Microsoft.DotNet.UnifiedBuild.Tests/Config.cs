// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
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
        UbBuildVersion = (string)(AppContext.GetData(BuildVersionSwitch) ?? throw new InvalidOperationException("Unified Build version must be specified"));
        TargetRid = (string)(AppContext.GetData(TargetRidSwitch) ?? throw new InvalidOperationException("Target RID must be specified"));
        PortableRid = (string)(AppContext.GetData(PortableRidSwitch) ?? throw new InvalidOperationException("Portable RID must be specified"));
        UbSdkArchivePath = (string)(AppContext.GetData(UbSdkArchivePathSwitch) ?? throw new InvalidOperationException("Unified Build SDK archive path must be specified"));
        TargetArchitecture = TargetRid.Split('-')[1];
        MsftSdkArchivePath = AppContext.GetData(MsftSdkArchivePathSwitch) as string ?? DownloadMsftSdkArchive().Result;
    }

    public const string ConfigSwitchPrefix = "Microsoft.DotNet.UnifiedBuild.Tests.";
    public const string BuildVersionSwitch = ConfigSwitchPrefix + nameof(UbBuildVersion);
    public const string TargetRidSwitch = ConfigSwitchPrefix + nameof(TargetRid);
    public const string PortableRidSwitch = ConfigSwitchPrefix + nameof(PortableRid);
    public const string UbSdkArchivePathSwitch = ConfigSwitchPrefix + nameof(UbSdkArchivePath);
    public const string MsftSdkArchivePathSwitch = ConfigSwitchPrefix + nameof(MsftSdkArchivePath);

    public string? MsftSdkArchivePath { get; }
    public string UbBuildVersion { get; }
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
        var channel = UbBuildVersion[..5] + "xx";
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
