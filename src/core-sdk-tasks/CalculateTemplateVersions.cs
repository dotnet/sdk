// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Cli.Build
{
    public class CalculateTemplateVersions : Task
    {
        [Required]
        public string AspNetCorePackageVersionTemplate { get; set; }

        [Required]
        public string GitCommitCount { get; set; }

        [Required]
        public string VersionSuffix { get; set; }

        [Output]
        public string BundledTemplateMSIVersion { get; set; }

        [Output]
        public string BundledTemplateInstallPath { get; set; }

        [Output]
        public string BundledTemplateMajorMinorVersion { get; set; }

        public override bool Execute()
        {
            var result = Calculate(AspNetCorePackageVersionTemplate, GitCommitCount, VersionSuffix);
            BundledTemplateMSIVersion = result.BundledTemplateMsiVersion;
            BundledTemplateInstallPath = result.BundledTemplateInstallPath;
            BundledTemplateMajorMinorVersion = result.BundledTemplateMajorMinorVersion;

            return true;
        }

        public static
            (string BundledTemplateMsiVersion,
            string BundledTemplateInstallPath,
            string BundledTemplateMajorMinorVersion) Calculate(string aspNetCorePackageVersionTemplate,
                string gitCommitCount, string versionSuffix)
        {
            (bool isStableVersion, string aspNetCoreVersionMajorMinorPatchVersion) =
                GetAspNetCoreVersionMajorMinorPatchVersion(aspNetCorePackageVersionTemplate);

            var bundledTemplateMsiVersion = $"{aspNetCoreVersionMajorMinorPatchVersion}.{gitCommitCount}";

            string bundledTemplateInstallPath = isStableVersion
                ? aspNetCoreVersionMajorMinorPatchVersion
                : $"{aspNetCoreVersionMajorMinorPatchVersion}-{versionSuffix}";

            var parsedAspNetCoreVersionMajorMinorPatchVersion =
                System.Version.Parse(aspNetCoreVersionMajorMinorPatchVersion);
            var bundledTemplateMajorMinorVersion =
                $"{parsedAspNetCoreVersionMajorMinorPatchVersion.Major}.{parsedAspNetCoreVersionMajorMinorPatchVersion.Minor}";

            return (
                bundledTemplateMsiVersion,
                bundledTemplateInstallPath,
                bundledTemplateMajorMinorVersion);
        }

        private static (bool isStableVersion, string aspNetCoreVersionMajorMinorPatchVersion)
            GetAspNetCoreVersionMajorMinorPatchVersion(string aspNetCorePackageVersionTemplate)
        {
            var indexOfAspNetCoreVersionPreReleaseSeparator = aspNetCorePackageVersionTemplate.IndexOf('-');
            string aspNetCoreVersionMajorMinorPatchVersion;
            if (indexOfAspNetCoreVersionPreReleaseSeparator != -1)
            {
                aspNetCoreVersionMajorMinorPatchVersion =
                    aspNetCorePackageVersionTemplate.Substring(0, indexOfAspNetCoreVersionPreReleaseSeparator);
            }
            else
            {
                aspNetCoreVersionMajorMinorPatchVersion = aspNetCorePackageVersionTemplate;
            }

            return (indexOfAspNetCoreVersionPreReleaseSeparator == -1, aspNetCoreVersionMajorMinorPatchVersion);
        }
    }
}
