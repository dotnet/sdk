// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
public class GetClosestOfficialSdk : Microsoft.Build.Utilities.Task
{
    [Required]
    public required string BuiltSdkPath { get; init; }

    [Output]
    public string ClosestOfficialSdkPath { get; set; } = "";

    public override bool Execute()
    {
        return Task.Run(ExecuteAsync).Result;
    }

    public async Task<bool> ExecuteAsync()
    {
        var (versionString, rid, extension) = GetInfoFromArchivePath(BuiltSdkPath);

        string downloadUrl = GetLatestOfficialSdkUrl(versionString, rid, extension);

        Log.LogMessage($"Downloading {downloadUrl}");
        var handler = new HttpClientHandler()
        {
            AllowAutoRedirect = false
        };
        var client = new HttpClient(handler);
        var redirectResponse = await client.GetAsync(downloadUrl);
        // aka.ms returns a 301 for valid redirects and a 302 to Bing for invalid URLs
        if (redirectResponse.StatusCode != HttpStatusCode.Moved)
        {
            Log.LogMessage(MessageImportance.High, $"Failed to download '{downloadUrl}': invalid aka.ms URL");
            return true;
        }
        var packageResponse = await client.GetAsync(redirectResponse.Headers.Location!);

        var packageUriPath = packageResponse.RequestMessage!.RequestUri!.LocalPath;
        string downloadedVersion = PathWithVersions.GetVersionInPath(packageUriPath).ToString();

        ClosestOfficialSdkPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + $".dotnet-sdk-{downloadedVersion}-{rid}{extension}");
        Log.LogMessage($"Copying {packageUriPath} to {ClosestOfficialSdkPath}");
        using (var file = File.Create(ClosestOfficialSdkPath))
        {
            await packageResponse.Content.CopyToAsync(file);
        }

        return true;
    }

    string GetLatestOfficialSdkUrl(string versionString, string rid, string extension)
    {
        // Channel in the form of 9.0.1xx
        var channel = versionString[..5] + "xx";
        return $"https://aka.ms/dotnet/{channel}/daily/dotnet-sdk-{rid}{extension}";
    }

    static (string Version, string Rid, string extension) GetInfoFromArchivePath(string path)
    {
        string extension;
        if (path.EndsWith(".tar.gz"))
        {
            extension = ".tar.gz";
        }
        else if (path.EndsWith(".zip"))
        {
            extension = ".zip";
        }
        else
        {
            throw new ArgumentException($"Invalid archive extension '{path}': must end with .tar.gz or .zip");
        }

        string filename = Path.GetFileName(path)[..^extension.Length];
        var dashDelimitedParts = filename.Split('-');
        var (rid, versionString) = dashDelimitedParts switch
        {
            ["dotnet", "sdk", var first, var second, var third, var fourth] when PathWithVersions.IsVersionString(first) => (third + '-' + fourth, first + '-' + second),
            ["dotnet", "sdk", var first, var second, var third, var fourth] when PathWithVersions.IsVersionString(third) => (first + '-' + second, third + '-' + fourth),
            _ => throw new ArgumentException($"Invalid archive file name '{filename}': file name should include full build full build full build full build full build full build full build full build full build version and rid")
        };

        return (versionString, rid, extension);
    }
}
