// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Build
{
    public class CalculateTemplateVersions : Task
    {
        //  Group BundledTemplates by TemplateFrameworkVersion
        //  In each group, get the version of the template with UseVersionForTemplateInstallPath=true
        //  From that version number, get the BundledTemplateInstallPath, BundledTemplateMajorMinorVersion, and BundledTemplateMajorMinorPatchVersion
        [Required]
        public ITaskItem [] BundledTemplates { get; set; }

        [Required]
        public string FullNugetVersion { get; set; }

        [Required]
        public string ProductMonikerRid { get; set; }

        public string InstallerExtension { get; set; }

        [Required]
        public int CombinedBuildNumberAndRevision { get; set; }


        //  Should be the BundledTemplates with BundledTemplateInstallPath metadata set to the value calculated for that group
        [Output]
        public ITaskItem [] BundledTemplatesWithInstallPaths { get; set; }

        //  For each group of templates (grouped by TemplateFrameworkVersion), this should be the following
        //  ItemSpec: NetCore60Templates
        //  TemplateBaseFilename: dotnet-60templates
        //  TemplatesMajorMinorVersion: 6.0 (from BundledTemplateMajorMinorVersion from group)
        //  InstallerUpgradeCode: Guid generated using GenerateGuidFromName, combining TemplateBaseFilename, FullNugetVersion, ProductMonikerRid, and InstallerExtension
        //  MSIVersion: Result of calling GenerateMsiVersionFromFullVersion logic with CombinedBuildNumberAndRevision and BundledTemplateMajorMinorPatchVersion from template group

        [Output]
        public ITaskItem [] TemplatesComponents { get; set; }

        private const int _patchVersionResetOffset = 1;

        public override bool Execute()
        {
            var groups = BundledTemplates.GroupBy(bt => bt.GetMetadata("TemplateFrameworkVersion"))
                .ToDictionary(g => g.Key, g =>
                {
                    var itemWithVersion = g.SingleOrDefault(i => i.GetMetadata("UseVersionForTemplateInstallPath").Equals("true", StringComparison.OrdinalIgnoreCase));
                    if (itemWithVersion == null)
                    {
                        throw new InvalidOperationException("Could not find single item with UseVersionForTemplateInstallPath for templates with TemplateFrameworkVersion: " + g.Key);
                    }

                    return Calculate(itemWithVersion.GetMetadata("PackageVersion"));
                });

            BundledTemplatesWithInstallPaths = BundledTemplates.Select(t =>
            {
                var templateWithInstallPath = new TaskItem(t);
                templateWithInstallPath.SetMetadata("BundledTemplateInstallPath", groups[t.GetMetadata("TemplateFrameworkVersion")].InstallPath);
                return templateWithInstallPath;
            }).ToArray();

            TemplatesComponents = groups.Select(g =>
            {
                string majorMinorWithoutDots = g.Value.MajorMinorVersion.Replace(".", "");
                var componentItem = new TaskItem($"NetCore{majorMinorWithoutDots}Templates");
                var templateBaseFilename = $"dotnet-{majorMinorWithoutDots}templates";
                componentItem.SetMetadata("TemplateBaseFilename", templateBaseFilename);
                componentItem.SetMetadata("TemplatesMajorMinorVersion", g.Value.MajorMinorVersion);
                var installerUpgradeCode = GenerateGuidFromName.GenerateGuid(string.Join("-", templateBaseFilename, FullNugetVersion, ProductMonikerRid) + InstallerExtension).ToString().ToUpper();
                componentItem.SetMetadata("InstallerUpgradeCode", installerUpgradeCode);
                componentItem.SetMetadata("MSIVersion", GenerateMsiVersionFromFullVersion.GenerateMsiVersion(CombinedBuildNumberAndRevision, g.Value.MajorMinorPatchVersion));

                var brandName = System.Version.Parse(g.Key).Major >= 5 ?
                    $"Microsoft .NET {g.Key} Templates" :
                    $"Microsoft .NET Core {g.Key} Templates";

                componentItem.SetMetadata("BrandNameWithoutVersion", brandName);

                return componentItem;
            }).ToArray();

            return true;
        }

        public static BundledTemplate Calculate(string aspNetCorePackageVersionTemplate)
        {
            var aspNetCoreTemplate = NuGetVersion.Parse(aspNetCorePackageVersionTemplate);
            NuGetVersion baseMajorMinorPatch = GetBaseMajorMinorPatch(aspNetCoreTemplate);
            string bundledTemplateInstallPath = aspNetCoreTemplate.IsPrerelease
                ? $"{baseMajorMinorPatch.Major}.{baseMajorMinorPatch.Minor}.{baseMajorMinorPatch.Patch}-{aspNetCoreTemplate.Release}"
                : $"{baseMajorMinorPatch.Major}.{baseMajorMinorPatch.Minor}.{baseMajorMinorPatch.Patch}";

            return new BundledTemplate
            {
                InstallPath = bundledTemplateInstallPath,
                MajorMinorVersion = $"{baseMajorMinorPatch.Major}.{baseMajorMinorPatch.Minor}",
                MajorMinorPatchVersion = $"{baseMajorMinorPatch.Major}.{baseMajorMinorPatch.Minor}.{baseMajorMinorPatch.Patch}"
            };
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

    public class BundledTemplate
    {
        public string InstallPath { get; set; }
        public string MajorMinorVersion { get; set; }
        public string MajorMinorPatchVersion { get; set; }
    }
}
