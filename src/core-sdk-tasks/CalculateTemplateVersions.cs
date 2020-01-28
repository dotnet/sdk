// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Versioning;

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

        private const int _patchVersionResetOffset = 1;

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
            var aspNetCoreTemplate = NuGetVersion.Parse(aspNetCorePackageVersionTemplate);

            NuGetVersion baseMajorMinorPatch = GetBaseMajorMinorPatch(aspNetCoreTemplate);

            string bundledTemplateInstallPath = aspNetCoreTemplate.IsPrerelease
                ? $"{baseMajorMinorPatch.Major}.{baseMajorMinorPatch.Minor}.{baseMajorMinorPatch.Patch}-{versionSuffix}"
                : $"{baseMajorMinorPatch.Major}.{baseMajorMinorPatch.Minor}.{baseMajorMinorPatch.Patch}";

            return (
                $"{baseMajorMinorPatch.Major}.{baseMajorMinorPatch.Minor}.{baseMajorMinorPatch.Patch}.{gitCommitCount}",
                bundledTemplateInstallPath,
                $"{baseMajorMinorPatch.Major}.{baseMajorMinorPatch.Minor}");
        }

        private static NuGetVersion GetBaseMajorMinorPatch(NuGetVersion aspNetCoreTemplate)
        {
            // due to historical bug https://github.com/dotnet/core-sdk/issues/6243
            // we need to increase patch version by one in order to "reset" existing install ComponentId
            // more in the above bug's detail.
            // There is no non-deterministic existing ComponentId under Major version 5.
            // so only apply the patch bump when below 5

            int basePatch =
                aspNetCoreTemplate.Major < 5
                ? aspNetCoreTemplate.Patch + _patchVersionResetOffset
                : aspNetCoreTemplate.Patch;

            var baseMajorMinorPatch = new NuGetVersion(aspNetCoreTemplate.Major, aspNetCoreTemplate.Minor,
                basePatch);
            return baseMajorMinorPatch;
        }
    }
}
