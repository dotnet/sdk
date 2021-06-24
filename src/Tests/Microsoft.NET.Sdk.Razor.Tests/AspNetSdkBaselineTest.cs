// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Razor.Tasks;
using Microsoft.NET.TestFramework;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class AspNetSdkBaselineTest : AspNetSdkTest
    {
        private static readonly JsonSerializerOptions BaselineSerializationOptions = new() { WriteIndented = true };
        
        private string _baselinesFolder;

#if GENERATE_SWA_BASELINES
        public static bool GenerateBaselines = true;
#else
        public static bool GenerateBaselines = false;
#endif

        private bool _generateBaselines = GenerateBaselines;

        public string BaselinesFolder => 
            _baselinesFolder ??= Path.Combine(TestContext.GetRepoRoot(), "src", "Tests", "Microsoft.NET.Sdk.Razor.Tests", "StaticWebAssetsBaselines");

        public TestAsset ProjectDirectory { get; set; }

        public AspNetSdkBaselineTest(ITestOutputHelper log) : base(log)
        {
        }

        public AspNetSdkBaselineTest(ITestOutputHelper log, bool generateBaselines) : base(log)
        {
            _generateBaselines = generateBaselines;
        }

        public StaticWebAssetsManifest LoadBuildManifest(string suffix = "", [CallerMemberName] string name = "")
        {
            if (_generateBaselines)
            {
                return default;
            }
            else
            {
                return StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(GetManifestPath(suffix, name, "Build")));
            }
        }

        public StaticWebAssetsManifest LoadPublishManifest(string suffix = "", [CallerMemberName] string name = "")
        {
            if (_generateBaselines)
            {
                return default;
            }
            else
            {
                return StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(GetManifestPath(suffix, name, "Publish")));
            }
        }

        public string[] LoadExpectedBuildFiles()
        {
            return default;
        }

        public string[] LoadExpectedPublishFiles()
        {
            return default;
        }

        internal void AssertManifest(
            StaticWebAssetsManifest manifest,
            StaticWebAssetsManifest expected,
            string suffix = "",
            [CallerMemberName] string name = "")
        {
            if (!_generateBaselines)
            {
                ApplyPathsToAssets(expected, ProjectDirectory.Path, TestContext.Current.NuGetCachePath);
                //Many of the properties in the manifest contain full paths, to avoid flakiness on the tests, we don't compare the full paths.
                manifest.Version.Should().Be(expected.Version);
                manifest.Source.Should().Be(expected.Source);
                manifest.BasePath.Should().Be(expected.BasePath);
                manifest.Mode.Should().Be(expected.Mode);
                manifest.ManifestType.Should().Be(expected.ManifestType);
                manifest.RelatedManifests.Select(rm => new ComparableManifest(rm.Identity, rm.Source, rm.Type))
                    .Should()
                    .BeEquivalentTo(expected.RelatedManifests.Select(rm => new ComparableManifest(rm.Identity, rm.Source, rm.Type)));
                manifest.DiscoveryPatterns.ShouldBeEquivalentTo(expected.DiscoveryPatterns);
            }
            else
            {
                var template = Templatize(manifest, ProjectDirectory.Path, TestContext.Current.NuGetCachePath);
                if (!Directory.Exists(Path.Combine(BaselinesFolder)))
                {
                    Directory.CreateDirectory(Path.Combine(BaselinesFolder));
                }

                File.WriteAllText(GetManifestPath(suffix, name, manifest.ManifestType), template);
            }
        }

        private string GetManifestPath(string suffix, string name, string manifestType)
        {
            return Path.Combine(BaselinesFolder, $"{name}{(!string.IsNullOrEmpty(suffix)? $"_{suffix}" : "")}.{manifestType}.staticwebassets.json");
        }

        private void ApplyPathsToAssets(
            StaticWebAssetsManifest manifest,
            string projectRoot,
            string restorePath)
        {
            foreach (var asset in manifest.Assets)
            {
                asset.Identity = asset.Identity.Replace("${ProjectRoot}", projectRoot).Replace('\\', Path.DirectorySeparatorChar);
                asset.Identity = asset.Identity.Replace("${RestorePath}", restorePath).Replace('\\', Path.DirectorySeparatorChar);

                asset.ContentRoot = asset.ContentRoot.Replace("${ProjectRoot}", projectRoot).Replace('\\', Path.DirectorySeparatorChar);
                asset.ContentRoot = asset.ContentRoot.Replace("${RestorePath}", restorePath).Replace('\\', Path.DirectorySeparatorChar);
            }

            foreach (var discovery in manifest.DiscoveryPatterns)
            {
                discovery.ContentRoot = discovery.ContentRoot.Replace("${ProjectRoot}", projectRoot).Replace('\\', Path.DirectorySeparatorChar);
                discovery.ContentRoot = discovery.ContentRoot.Replace("${RestorePath}", restorePath).Replace('\\', Path.DirectorySeparatorChar);
            }

            foreach (var relatedManifest in manifest.RelatedManifests)
            {
                relatedManifest.Identity = relatedManifest.Identity.Replace("${ProjectRoot}", projectRoot).Replace('\\', Path.DirectorySeparatorChar);
            }
        }

        private static string Templatize(StaticWebAssetsManifest manifest, string projectRoot, string restorePath)
        {
            foreach (var asset in manifest.Assets)
            {
                asset.Identity = asset.Identity.Replace(projectRoot, "${ProjectRoot}").Replace(Path.DirectorySeparatorChar, '\\');
                asset.Identity = asset.Identity.Replace(restorePath, "${RestorePath}").Replace(Path.DirectorySeparatorChar, '\\');

                asset.ContentRoot = asset.ContentRoot.Replace(projectRoot, "${ProjectRoot}").Replace(Path.DirectorySeparatorChar, '\\');
                asset.ContentRoot = asset.ContentRoot.Replace(restorePath, "${RestorePath}").Replace(Path.DirectorySeparatorChar, '\\');
            }

            foreach (var discovery in manifest.DiscoveryPatterns)
            {
                discovery.ContentRoot = discovery.ContentRoot.Replace(projectRoot, "${ProjectRoot}").Replace(Path.DirectorySeparatorChar, '\\');
                discovery.ContentRoot = discovery.ContentRoot.Replace(restorePath, "${RestorePath}").Replace(Path.DirectorySeparatorChar, '\\');
            }

            foreach (var relatedManifest in manifest.RelatedManifests)
            {
                relatedManifest.Identity = relatedManifest.Identity.Replace(projectRoot, "${ProjectRoot}").Replace(Path.DirectorySeparatorChar, '\\');
            }

            return JsonSerializer.Serialize(manifest, BaselineSerializationOptions);
        }

        private record ComparableManifest(string Identity, string Source, string Type);
    }
}
