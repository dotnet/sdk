// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader
{
    internal class PackageSourceLocation
    {
        public PackageSourceLocation(
            FilePath? nugetConfig = null,
            DirectoryPath? rootConfigDirectory = null, 
            string[] overrideSourceFeeds = null)
        {
            NugetConfig = nugetConfig;
            RootConfigDirectory = rootConfigDirectory;
            ExpandLocalFeedAndAssign(overrideSourceFeeds);
        }

        private void ExpandLocalFeedAndAssign(string[] overrideSourceFeeds)
        {
            if (overrideSourceFeeds != null)
            {
                string[] localFeedsThatIsRooted = new string[overrideSourceFeeds.Length];
                for (int index = 0; index < overrideSourceFeeds.Length; index++)
                {
                    string feed = overrideSourceFeeds[index];
                    if (!Uri.IsWellFormedUriString(feed, UriKind.Absolute) && !Path.IsPathRooted(feed))
                    {
                        localFeedsThatIsRooted[index] = (Path.GetFullPath(feed));
                    }
                    else
                    {
                        localFeedsThatIsRooted[index] = (feed);
                    }
                }

                OverrideSourceFeeds = localFeedsThatIsRooted;
            }
            else
            {
                OverrideSourceFeeds = Array.Empty<string>();
            }
        }

        public FilePath? NugetConfig { get; }
        public DirectoryPath? RootConfigDirectory { get; }
        public string[] OverrideSourceFeeds { get; set; }
    }
}
