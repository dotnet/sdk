// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Build.Framework;

public class GetClosestOfficialSdk : GetClosestArchive
{
    protected override string ArchiveName => "dotnet-sdk";

    HttpClient client = new HttpClient(new HttpClientHandler() { AllowAutoRedirect = false });

    private string? closestVersion;
    private string? closestUrl;

    public override async Task<string?> GetClosestOfficialArchiveUrl()
    {
        // Channel in the form of 9.0.1xx
        var channel = BuiltVersion[..5] + "xx";
        var akaMsUrl = $"https://aka.ms/dotnet/{channel}/daily/{ArchiveName}-{BuiltRid}{ArchiveExtension}";
        var redirectResponse = await client.GetAsync(akaMsUrl, CancellationToken);
        // aka.ms returns a 301 for valid redirects and a 302 to Bing for invalid URLs
        if (redirectResponse.StatusCode != HttpStatusCode.Moved)
        {
            Log.LogMessage(MessageImportance.High, $"Failed to find package at '{akaMsUrl}': invalid aka.ms URL");
            return null;
        }
        closestUrl = redirectResponse.Headers.Location!.ToString();
        closestVersion = VersionIdentifier.GetVersion(closestUrl);
        return closestUrl;
    }

    public override async Task<string?> GetClosestOfficialArchiveVersion()
    {
        if (closestUrl is not null)
        {
            return closestVersion;
        }
        _ = await GetClosestOfficialArchiveUrl();
        return closestVersion;
    }
}
