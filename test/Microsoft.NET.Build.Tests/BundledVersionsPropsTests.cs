// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using NuGet.Frameworks;

namespace Microsoft.NET.Build.Tests
{
    /// <summary>
    /// Verifies the shape of the Known* items in Microsoft.NETCoreSdk.BundledVersions.props, which is generated
    /// by batching over the BundledTargetFramework catalog in
    /// src/Layout/redist/targets/GenerateBundledVersions.targets. These assertions guard against a target
    /// framework accidentally gaining or losing an item (or a piece of metadata) when that catalog is edited.
    /// </summary>
    [TestClass]
    public class BundledVersionsPropsTests : SdkTest
    {
        private static ILookup<string, XElement> GetItemsByTargetFramework(string itemType)
        {
            var bundledVersionsPropsPath = Path.Combine(
                SdkTestContext.Current.ToolsetUnderTest.SdkFolderUnderTest,
                "Microsoft.NETCoreSdk.BundledVersions.props");

            File.Exists(bundledVersionsPropsPath).Should().BeTrue($"'{bundledVersionsPropsPath}' should exist");

            return XDocument.Load(bundledVersionsPropsPath)
                .Root
                .Elements()
                .SelectMany(itemGroup => itemGroup.Elements())
                .Where(item => item.Name.LocalName.Equals(itemType, StringComparison.Ordinal))
                .ToLookup(item => (string)item.Attribute("TargetFramework"));
        }

        private static IEnumerable<string> TargetFrameworks =>
            GetItemsByTargetFramework("KnownFrameworkReference")
                .Where(group => group.Any(item => (string)item.Attribute("Include") == "Microsoft.NETCore.App"))
                .Select(group => group.Key);

        private static Version VersionOf(string targetFramework) => NuGetFramework.Parse(targetFramework).Version;

        [TestMethod]
        public void ThereIsAKnownFrameworkReferenceForEveryTargetFramework()
        {
            var knownFrameworkReferences = GetItemsByTargetFramework("KnownFrameworkReference");

            //  Every shipped target framework should be in the catalog. The target framework of the SDK being
            //  built isn't listed here so that this test doesn't need to be updated for each new release.
            TargetFrameworks.Should().Contain(new[] { "netcoreapp3.0", "netcoreapp3.1", "net5.0", "net6.0", "net7.0", "net8.0", "net9.0", "net10.0" });

            foreach (var targetFramework in TargetFrameworks)
            {
                var includes = knownFrameworkReferences[targetFramework].Select(item => (string)item.Attribute("Include"));

                includes.Should().BeEquivalentTo(new[]
                {
                    "Microsoft.NETCore.App",
                    "Microsoft.WindowsDesktop.App",
                    "Microsoft.WindowsDesktop.App.WPF",
                    "Microsoft.WindowsDesktop.App.WindowsForms",
                    "Microsoft.AspNetCore.App"
                }, $"{targetFramework} should have a KnownFrameworkReference for each shared framework");

                foreach (var knownFrameworkReference in knownFrameworkReferences[targetFramework])
                {
                    foreach (var requiredMetadata in new[]
                    {
                        "RuntimeFrameworkName",
                        "DefaultRuntimeFrameworkVersion",
                        "LatestRuntimeFrameworkVersion",
                        "TargetingPackName",
                        "TargetingPackVersion",
                        "RuntimePackNamePatterns",
                        "RuntimePackRuntimeIdentifiers"
                    })
                    {
                        ((string)knownFrameworkReference.Attribute(requiredMetadata)).Should().NotBeNullOrEmpty(
                            $"{knownFrameworkReference.Attribute("Include")} for {targetFramework} should specify {requiredMetadata}");
                    }
                }
            }
        }

        [TestMethod]
        public void ToolPacksAreGeneratedForTheExpectedTargetFrameworks()
        {
            var appHostPacks = GetItemsByTargetFramework("KnownAppHostPack");
            var ilLinkPacks = GetItemsByTargetFramework("KnownILLinkPack");
            var crossgen2Packs = GetItemsByTargetFramework("KnownCrossgen2Pack");
            var ilCompilerPacks = GetItemsByTargetFramework("KnownILCompilerPack");
            var webAssemblySdkPacks = GetItemsByTargetFramework("KnownWebAssemblySdkPack");
            var aspNetCorePacks = GetItemsByTargetFramework("KnownAspNetCorePack");
            var runtimePacks = GetItemsByTargetFramework("KnownRuntimePack");

            foreach (var targetFramework in TargetFrameworks)
            {
                var version = VersionOf(targetFramework);

                appHostPacks[targetFramework].Should().HaveCount(1, $"{targetFramework} should have a KnownAppHostPack");
                ilLinkPacks[targetFramework].Should().HaveCount(1, $"{targetFramework} should have a KnownILLinkPack");

                //  Crossgen2 was introduced in .NET 5, and NativeAOT (ILCompiler) in .NET 7.
                crossgen2Packs[targetFramework].Should().HaveCount(version.Major >= 5 ? 1 : 0, $"KnownCrossgen2Pack for {targetFramework}");
                ilCompilerPacks[targetFramework].Should().HaveCount(version.Major >= 7 ? 1 : 0, $"KnownILCompilerPack for {targetFramework}");

                //  The WebAssembly SDK pack applies to .NET 6 and up, the ASP.NET Core assets pack to .NET 10 and up.
                webAssemblySdkPacks[targetFramework].Should().HaveCount(version.Major >= 6 ? 1 : 0, $"KnownWebAssemblySdkPack for {targetFramework}");
                aspNetCorePacks[targetFramework].Should().HaveCount(version.Major >= 10 ? 1 : 0, $"KnownAspNetCorePack for {targetFramework}");

                //  The Mono runtime pack applies to .NET 6 and up, the NativeAOT runtime pack to .NET 8 and up.
                var runtimePackLabels = runtimePacks[targetFramework].Select(item => (string)item.Attribute("RuntimePackLabels"));
                var expectedRuntimePackLabels = new List<string>();
                if (version.Major >= 8)
                {
                    expectedRuntimePackLabels.Add("NativeAOT");
                }
                if (version.Major >= 6)
                {
                    expectedRuntimePackLabels.Add("Mono");
                }

                runtimePackLabels.Should().BeEquivalentTo(expectedRuntimePackLabels, $"KnownRuntimePack items for {targetFramework}");
            }
        }

        [TestMethod]
        public void PortableRuntimeIdentifiersAreOnlySetForRecentTargetFrameworks()
        {
            foreach (var (itemType, portableMetadata, runtimeIdentifiersMetadata) in new[]
            {
                ("KnownCrossgen2Pack", "Crossgen2PortableRuntimeIdentifiers", "Crossgen2RuntimeIdentifiers"),
                ("KnownILCompilerPack", "ILCompilerPortableRuntimeIdentifiers", "ILCompilerRuntimeIdentifiers")
            })
            {
                var packs = GetItemsByTargetFramework(itemType);

                foreach (var targetFramework in TargetFrameworks)
                {
                    foreach (var pack in packs[targetFramework])
                    {
                        ((string)pack.Attribute(runtimeIdentifiersMetadata)).Should().NotBeNullOrEmpty(
                            $"{itemType} for {targetFramework} should specify {runtimeIdentifiersMetadata}");

                        //  The portable runtime identifier list was added in .NET 10.
                        var hasPortableRuntimeIdentifiers = !string.IsNullOrEmpty((string)pack.Attribute(portableMetadata));
                        hasPortableRuntimeIdentifiers.Should().Be(VersionOf(targetFramework).Major >= 10,
                            $"{itemType} for {targetFramework} {portableMetadata}");
                    }
                }
            }
        }
    }
}
