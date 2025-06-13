// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader;

internal class PackageSourceLocation
{
    public PackageSourceLocation(
        FilePath? nugetConfig = null,
        DirectoryPath? rootConfigDirectory = null,
        string[] sourceFeedOverrides = null,
        string[] additionalSourceFeeds = null,
        string basePath = null)
    {
        basePath = basePath ?? Directory.GetCurrentDirectory();

        NugetConfig = nugetConfig;
        RootConfigDirectory = rootConfigDirectory;
        // Overrides other feeds
        SourceFeedOverrides = ExpandLocalFeed(sourceFeedOverrides, basePath);
        // Feeds to be using in addition to config
        AdditionalSourceFeed = ExpandLocalFeed(additionalSourceFeeds, basePath);
    }

    public FilePath? NugetConfig { get; }
    public DirectoryPath? RootConfigDirectory { get; }
    public string[] SourceFeedOverrides { get; private set; }
    public string[] AdditionalSourceFeed { get; private set; }

    private static string[] ExpandLocalFeed(string[] sourceFeedOverrides, string basePath)
    {
        if (sourceFeedOverrides != null)
        {
            string[] localFeedsThatIsRooted = new string[sourceFeedOverrides.Length];
            for (int index = 0; index < sourceFeedOverrides.Length; index++)
            {
                string feed = sourceFeedOverrides[index];
                if (!Uri.IsWellFormedUriString(feed, UriKind.Absolute) && !Path.IsPathRooted(feed))
                {
                    localFeedsThatIsRooted[index] = Path.GetFullPath(feed, basePath);
                }
                else
                {
                    localFeedsThatIsRooted[index] = feed;
                }
            }

            return localFeedsThatIsRooted;
        }
        else
        {
            return [];
        }
    }
}
