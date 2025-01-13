// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils
{
    internal class DotnetVersionFile
    {
        public bool Exists { get; init; }

        public string? CommitSha { get; init; }

        public string? BuildNumber { get; init; }

        public string? FullNugetVersion { get; init; }

        public string? SdkFeatureBand { get; init; }

        /// <summary>
        /// The runtime identifier (rid) that this CLI was built for.
        /// </summary>
        /// <remarks>
        /// This is different than RuntimeInformation.RuntimeIdentifier because the
        /// BuildRid is a RID that is guaranteed to exist and works on the current machine. The
        /// RuntimeInformation.RuntimeIdentifier may be for a new version of the OS that
        /// doesn't have full support yet.
        /// </remarks>
        public string? BuildRid { get; init; }

        public DotnetVersionFile(string versionFilePath)
        {
            Exists = File.Exists(versionFilePath);

            if (Exists)
            {
                IEnumerable<string> lines = File.ReadLines(versionFilePath);

                int index = 0;
                foreach (string line in lines)
                {
                    if (index == 0)
                    {
                        CommitSha = line.Substring(0, 10);
                    }
                    else if (index == 1)
                    {
                        BuildNumber = line;
                    }
                    else if (index == 2)
                    {
                        BuildRid = line;
                    }
                    else if (index == 3)
                    {
                        FullNugetVersion = line;
                    }
                    else if (index == 4)
                    {
                        SdkFeatureBand = line;
                    }
                    else
                    {
                        break;
                    }

                    index++;
                }
            }
        }
    }
}
