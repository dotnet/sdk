// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Versioning;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Use the runtime in dotnet/sdk instead of in the stage 0 to avoid circular dependency.
    /// If there is a change depended on the latest runtime. Without override the runtime version in BundledNETCoreAppPackageVersion
    /// we would need to somehow get this change in without the test, and then insertion dotnet/installer
    /// and then update the stage 0 back.
    ///
    /// Override NETCoreSdkVersion to stage 0 sdk version like 6.0.100-dev
    /// Override NETCoreSdkRuntimeIdentifier and NETCoreSdkPortableRuntimeIdentifier to match the target RID
    ///
    /// Use a task to override since it was generated as a string literal replace anyway.
    /// And using C# can have better error when anything goes wrong.
    /// </summary>
    public sealed class OverrideAndCreateBundledNETCoreAppPackageVersion : Task
    {
        private static string _messageWhenMismatch =
            "{0} version {1} does not match BundledNETCoreAppPackageVersion {2}. " +
            "The schema of https://github.com/dotnet/installer/blob/main/src/redist/targets/GenerateBundledVersions.targets might change. " +
            "We need to ensure we can swap the runtime version from what's in stage0 to what dotnet/sdk used successfully";

        [Required] public string Stage0MicrosoftNETCoreAppRefPackageVersionPath { get; set; }

        [Required] public string MicrosoftNETCoreAppRefPackageVersion { get; set; }

        [Required] public string NewSDKVersion { get; set; }

        [Required] public string TargetRid { get; set; }

        [Required] public string OutputPath { get; set; }

        public override bool Execute()
        {
            File.WriteAllText(OutputPath,
                ExecuteInternal(
                    File.ReadAllText(Stage0MicrosoftNETCoreAppRefPackageVersionPath),
                    MicrosoftNETCoreAppRefPackageVersion,
                    NewSDKVersion,
                    TargetRid,
                    Log));
            return true;
        }

        public static string ExecuteInternal(
            string stage0MicrosoftNETCoreAppRefPackageVersionContent,
            string microsoftNETCoreAppRefPackageVersion,
            string newSDKVersion,
            string targetRid,
            TaskLoggingHelper log)
        {
            var projectXml = XDocument.Parse(stage0MicrosoftNETCoreAppRefPackageVersionContent);

            var ns = projectXml.Root.Name.Namespace;

            var propertyGroup = projectXml.Root.Elements(ns + "PropertyGroup").First();            

            propertyGroup.Element(ns + "NETCoreSdkVersion").Value = newSDKVersion;
            propertyGroup.Element(ns + "NETCoreSdkRuntimeIdentifier").Value = targetRid;
            propertyGroup.Element(ns + "NETCoreSdkPortableRuntimeIdentifier").Value = targetRid;


            var originalBundledNETCoreAppPackageVersion = propertyGroup.Element(ns + "BundledNETCoreAppPackageVersion").Value;
            var parsedOriginalBundledPackageVersion = SemanticVersion.Parse(originalBundledNETCoreAppPackageVersion);
            var parsedMicrosoftNETCoreAppRefPackageVersion =
                SemanticVersion.Parse(microsoftNETCoreAppRefPackageVersion);

            // In the case where we have a new major version, it'll come in first through the dotnet/runtime flow of the
            // SDK's own package references. The Stage0 SDK's bundled version props file will still be on the older major version
            // (and the older TFM that goes along with that). If we just replaced the bundled version with the new major version,
            // apps that target the 'older' TFM would fail to build. So we need to keep the bundled version from the existing
            // bundledversions.props in this one specific case.

            var newBundledPackageVersion =
                parsedOriginalBundledPackageVersion.Major == parsedMicrosoftNETCoreAppRefPackageVersion.Major
                ? microsoftNETCoreAppRefPackageVersion
                : originalBundledNETCoreAppPackageVersion;

            propertyGroup.Element(ns + "BundledNETCoreAppPackageVersion").Value = newBundledPackageVersion;

            var isNETServicing = IsNETServicing(originalBundledNETCoreAppPackageVersion);
            var currentTargetFramework = $"net{parsedMicrosoftNETCoreAppRefPackageVersion.Major}.0";

            void CheckAndReplaceElement(XElement element)
            {
                if (element.Value != originalBundledNETCoreAppPackageVersion)
                {
                    throw new InvalidOperationException(string.Format(
                        _messageWhenMismatch,
                        element.ToString(), element.Value, originalBundledNETCoreAppPackageVersion));
                }

                log.LogMessage(MessageImportance.High,
                    $"Replacing element {element.Name} value '{element.Value}' with '{newBundledPackageVersion}'");
                element.Value = newBundledPackageVersion;
            }

            void CheckAndReplaceAttribute(XAttribute attribute)
            {
                if (attribute.Value != originalBundledNETCoreAppPackageVersion)
                {
                    throw new InvalidOperationException(string.Format(
                        _messageWhenMismatch,
                        attribute.Parent.ToString() + " --- " + attribute.ToString(), attribute.Value,
                        originalBundledNETCoreAppPackageVersion));
                }

                log.LogMessage(MessageImportance.High,
                    $"Replacing attribute {attribute.Name} value '{attribute.Value}' with '{newBundledPackageVersion}' in element {attribute.Parent.Name}");
                attribute.Value = newBundledPackageVersion;
            }

            if (!isNETServicing)
            {
                CheckAndReplaceElement(propertyGroup.Element(ns + "BundledNETCorePlatformsPackageVersion"));
            }

            var itemGroup = projectXml.Root.Elements(ns + "ItemGroup").First();

            if (!isNETServicing)
            {
                foreach (var element in itemGroup.Elements(ns + "KnownFrameworkReference")
                    .Where(e => e.Attribute("TargetFramework")?.Value == currentTargetFramework))
                {
                    CheckAndReplaceAttribute(element.Attribute("DefaultRuntimeFrameworkVersion"));
                    CheckAndReplaceAttribute(element.Attribute("TargetingPackVersion"));
                }
            }

            foreach (var element in itemGroup.Elements(ns + "KnownFrameworkReference")
                .Where(e => e.Attribute("TargetFramework")?.Value == currentTargetFramework))
            {
                CheckAndReplaceAttribute(element.Attribute("LatestRuntimeFrameworkVersion"));
            }
            foreach (var element in itemGroup.Elements(ns + "KnownAppHostPack")
                .Where(e => e.Attribute("TargetFramework")?.Value == currentTargetFramework))
            {
                CheckAndReplaceAttribute(element.Attribute("AppHostPackVersion"));
            }
            
            foreach (var element in itemGroup.Elements(ns + "KnownCrossgen2Pack")
                .Where(e => e.Attribute("TargetFramework")?.Value == currentTargetFramework))
            {
                CheckAndReplaceAttribute(element.Attribute("Crossgen2PackVersion"));
            }
            
            foreach (var element in itemGroup.Elements(ns + "KnownILCompilerPack")
                .Where(e => e.Attribute("TargetFramework")?.Value == currentTargetFramework))
            {
                CheckAndReplaceAttribute(element.Attribute("ILCompilerPackVersion"));
            }
            
            foreach (var element in itemGroup.Elements(ns + "KnownILLinkPack")
                .Where(e => e.Attribute("TargetFramework")?.Value == currentTargetFramework))
            {
                CheckAndReplaceAttribute(element.Attribute("ILLinkPackVersion"));
            }
            
            // web assembly packs always use the latest regardless of the TFM
            foreach (var element in itemGroup.Elements(ns + "KnownWebAssemblySdkPack"))
            {
                CheckAndReplaceAttribute(element.Attribute("WebAssemblySdkPackVersion"));
            }
            
            foreach (var element in itemGroup.Elements(ns + "KnownAspNetCorePack")
                .Where(e => e.Attribute("TargetFramework")?.Value == currentTargetFramework))
            {
                CheckAndReplaceAttribute(element.Attribute("AspNetCorePackVersion"));
            }

            foreach (var element in itemGroup.Elements(ns + "KnownRuntimePack")
                .Where(e => e.Attribute("TargetFramework")?.Value == currentTargetFramework))
            {
                CheckAndReplaceAttribute(element.Attribute("LatestRuntimeFrameworkVersion"));
            }

            return projectXml.ToString();
        }

        /// <summary>
        /// For SDK servicing, few Attributes like "DefaultRuntimeFrameworkVersion" does not use the latest flowed version
        /// so there is no need to replace them.
        /// </summary>
        /// <returns></returns>
        private static bool IsNETServicing(string netVersion)
        {
            var parsedSdkVersion = NuGet.Versioning.NuGetVersion.Parse(netVersion);

            return !parsedSdkVersion.IsPrerelease;
        }
    }
}
