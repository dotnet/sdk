// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using NuGet.Versioning;

namespace Microsoft.DotNet.Build.Tasks
{
    public class GenerateMsiVersionFromFullVersion : Task
    {
        [Required]
        public int VersionRevision { get; set; }

        [Required]
        public string VersionMajorMinorPatch { get; set; }

        [Output]
        public string MsiVersion { get; set; }

        public override bool Execute()
        {
            MsiVersion = GenerateMsiVersion(VersionRevision, VersionMajorMinorPatch);

            return true;
        }

        public static string GenerateMsiVersion(int versionRevision, string versionMajorMinorPatch)
        {
            var parsedVersion = NuGetVersion.Parse(versionMajorMinorPatch);

            var buildVersion = new Version()
            {
                Major = parsedVersion.Major,
                Minor = parsedVersion.Minor,
                Patch = parsedVersion.Patch,
                VersionRevision = versionRevision
            };

            return buildVersion.GenerateMsiVersion();
        }
    }
}
