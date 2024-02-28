// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;

public class GetClosestOfficialSdk : Microsoft.Build.Utilities.Task, ICancelableTask
{
    [Required]
    public required string BuiltSdkPath { get; init; }

    [Output]
    public string ClosestOfficialSdkPath { get; set; } = "";

    public override bool Execute()
    {
        return Task.Run(ExecuteAsync).Result;
    }

    private CancellationTokenSource _cancellationTokenSource = new();
    private CancellationToken cancellationToken => _cancellationTokenSource.Token;
    public void Cancel()
    {
        _cancellationTokenSource.Cancel();
    }

    public async Task<bool> ExecuteAsync()
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (versionString, rid, extension) = Archive.GetInfoFromArchivePath(BuiltSdkPath);

        string downloadUrl = GetLatestOfficialSdkUrl(versionString, rid, extension);

        Log.LogMessage(MessageImportance.High, $"Downloading {downloadUrl}");
        var handler = new HttpClientHandler()
        {
            AllowAutoRedirect = false
        };
        var client = new HttpClient(handler);
        var redirectResponse = await client.GetAsync(downloadUrl, cancellationToken);
        // aka.ms returns a 301 for valid redirects and a 302 to Bing for invalid URLs
        if (redirectResponse.StatusCode != HttpStatusCode.Moved)
        {
            Log.LogMessage(MessageImportance.High, $"Failed to download '{downloadUrl}': invalid aka.ms URL");
            return true;
        }
        var packageResponse = await client.GetAsync(redirectResponse.Headers.Location!, cancellationToken);

        var packageUriPath = packageResponse.RequestMessage!.RequestUri!.LocalPath;
        string downloadedVersion = PathWithVersions.GetVersionInPath(packageUriPath).ToString();

        ClosestOfficialSdkPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + $".dotnet-sdk-{downloadedVersion}-{rid}{extension}");
        Log.LogMessage($"Copying {packageUriPath} to {ClosestOfficialSdkPath}");
        using (var file = File.Create(ClosestOfficialSdkPath))
        {
            await packageResponse.Content.CopyToAsync(file, cancellationToken);
        }

        return true;
    }

    string GetLatestOfficialSdkUrl(string versionString, string rid, string extension)
    {
        // Channel in the form of 9.0.1xx
        var channel = versionString[..5] + "xx";
        return $"https://aka.ms/dotnet/{channel}/daily/dotnet-sdk-{rid}{extension}";
    }

}
