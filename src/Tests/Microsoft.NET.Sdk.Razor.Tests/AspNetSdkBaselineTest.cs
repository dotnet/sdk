// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

        public AspNetSdkBaselineTest(ITestOutputHelper log) : base(log)
        {
            var assembly = Assembly.GetCallingAssembly();
            var testAssemblyMetadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
            RuntimeVersion = testAssemblyMetadata.SingleOrDefault(a => a.Key == "NetCoreAppRuntimePackageVersion").Value;
            DefaultPackageVersion = testAssemblyMetadata.SingleOrDefault(a => a.Key == "DefaultTestBaselinePackageVersion").Value;
        }

        public AspNetSdkBaselineTest(ITestOutputHelper log, bool generateBaselines) : this(log)
        {
            _generateBaselines = generateBaselines;
        }

        public TestAsset ProjectDirectory { get; set; }

        public string RuntimeVersion { get; set; }

        public string DefaultPackageVersion { get; set; }

        public string BaselinesFolder =>
            _baselinesFolder ??= ComputeBaselineFolder();

        protected virtual string ComputeBaselineFolder() =>
            Path.Combine(TestContext.GetRepoRoot(), "src", "Tests", "Microsoft.NET.Sdk.Razor.Tests", "StaticWebAssetsBaselines");

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

        protected void AssertBuildAssets(
            StaticWebAssetsManifest manifest,
            string outputFolder,
            string intermediateOutputPath,
            string suffix = "",
            [CallerMemberName] string name = "")
        {
            var fileEnumerationOptions = new EnumerationOptions { RecurseSubdirectories = true };
            var wwwRootFolder = Path.Combine(outputFolder, "wwwroot");
            var wwwRootFiles = Directory.Exists(wwwRootFolder) ?
                Directory.GetFiles(wwwRootFolder, "*", fileEnumerationOptions) :
                Array.Empty<string>();

            var computedFiles = manifest.Assets
                .Where(a => a.SourceType is StaticWebAsset.SourceTypes.Computed &&
                            a.AssetKind is not StaticWebAsset.AssetKinds.Publish);

            // We keep track of assets that need to be copied to the output folder.
            // In addition to that, we copy assets that are defined somewhere different
            // from their content root folder when the content root does not match the output folder.
            // We do this to allow copying things like Publish assets to temporary locations during the
            // build process if they are later on going to be transformed.
            var copyToOutputDirectoryFiles = manifest.Assets
                .Where(a => a.ShouldCopyToOutputDirectory())
                .Select(a => Path.Combine(outputFolder, "wwwroot", a.RelativePath))
                .Concat(manifest.Assets
                    .Where(a => !a.HasContentRoot(Path.Combine(outputFolder, "wwwroot")) && File.Exists(a.Identity) && !File.Exists(Path.Combine(a.ContentRoot, a.RelativePath)))
                    .Select(a => Path.Combine(a.ContentRoot, a.RelativePath)));

            if (!_generateBaselines)
            {
                var expected = LoadExpectedFilesBaseline(manifest.ManifestType, outputFolder, intermediateOutputPath, suffix, name);

                var existingFiles = wwwRootFiles.Concat(computedFiles.Select(f => f.Identity)).Concat(copyToOutputDirectoryFiles)
                    .Distinct()
                    .OrderBy(f => f, StringComparer.Ordinal)
                    .ToArray();

                existingFiles.ShouldBeEquivalentTo(expected);
            }
            else
            {
                var templatizedFiles = TemplatizeExpectedFiles(
                    wwwRootFiles
                        .Concat(computedFiles.Select(f => f.Identity))
                        .Concat(copyToOutputDirectoryFiles)
                        .Distinct()
                        .OrderBy(f => f, StringComparer.Ordinal)
                        .ToArray(),
                    TestContext.Current.NuGetCachePath,
                    outputFolder,
                    intermediateOutputPath);

                File.WriteAllText(
                    GetExpectedFilesPath(suffix, name, manifest.ManifestType),
                    JsonSerializer.Serialize(templatizedFiles, BaselineSerializationOptions));
            }
        }

        protected void AssertPublishAssets(
            StaticWebAssetsManifest manifest,
            string publishFolder,
            string intermediateOutputPath,
            string suffix = "",
            [CallerMemberName] string name = "")
        {
            var fileEnumerationOptions = new EnumerationOptions { RecurseSubdirectories = true };
            string wwwRootFolder = Path.Combine(publishFolder, "wwwroot");
            var wwwRootFiles = Directory.Exists(wwwRootFolder) ?
                Directory.GetFiles(wwwRootFolder, "*", fileEnumerationOptions) :
                Array.Empty<string>();

            // Computed publish assets must exist on disk (we do this check to quickly identify when something is not being
            // generated vs when its being copied to the wrong place)
            var computedFiles = manifest.Assets
                .Where(a => a.SourceType is StaticWebAsset.SourceTypes.Computed &&
                            a.AssetKind is not StaticWebAsset.AssetKinds.Build);

            // For assets that are copied to the publish folder, the path is always based on
            // the wwwroot folder, the relative path and the base path for project or package
            // assets.
            var copyToPublishDirectoryFiles = manifest.Assets
                .Where(a => !string.Equals(a.SourceId, manifest.Source, StringComparison.Ordinal) ||
                            !string.Equals(a.AssetMode, StaticWebAsset.AssetModes.Reference))
                .Select(a => a.ComputeTargetPath(wwwRootFolder, Path.DirectorySeparatorChar));

            var existingFiles = wwwRootFiles.Concat(computedFiles.Select(f => f.Identity)).Concat(copyToPublishDirectoryFiles)
                .Distinct()
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToArray();

            if (!_generateBaselines)
            {
                var expected = LoadExpectedFilesBaseline(manifest.ManifestType, publishFolder, intermediateOutputPath, suffix, name);
                existingFiles.ShouldBeEquivalentTo(expected);
            }
            else
            {
                var templatizedFiles = TemplatizeExpectedFiles(
                    wwwRootFiles
                        .Concat(computedFiles.Select(f => f.Identity))
                        .Concat(copyToPublishDirectoryFiles)
                        .Distinct()
                        .OrderBy(f => f, StringComparer.Ordinal)
                        .ToArray(),
                    TestContext.Current.NuGetCachePath,
                    publishFolder,
                    intermediateOutputPath);

                File.WriteAllText(
                    GetExpectedFilesPath(suffix, name, manifest.ManifestType),
                    JsonSerializer.Serialize(templatizedFiles, BaselineSerializationOptions));
            }
        }

        public string[] LoadExpectedFilesBaseline(
            string type,
            string buildOrPublishPath,
            string intermediateOutputPath,
            string suffix,
            string name)
        {
            var filesBaselinePath = GetExpectedFilesPath(suffix, name, type);
            if (!_generateBaselines)
            {
                return ApplyPathsToTemplatedFilePaths(
                    JsonSerializer.Deserialize<string[]>(File.ReadAllBytes(filesBaselinePath)),
                    TestContext.Current.NuGetCachePath,
                    buildOrPublishPath,
                    intermediateOutputPath)
                    .ToArray();
            }
            else
            {

                return Array.Empty<string>();
            }
        }

        private IEnumerable<string> TemplatizeExpectedFiles(
            IEnumerable<string> files,
            string restorePath,
            string buildOrPublishFolder,
            string intermediateOutputPath) =>
                files.Select(f => f.Replace(restorePath, "${RestorePath}")
                               .Replace(RuntimeVersion, "${RuntimeVersion}")
                               .Replace(DefaultPackageVersion, "${PackageVersion}")
                               .Replace(buildOrPublishFolder, "${OutputPath}")
                               .Replace(intermediateOutputPath, "${IntermediateOutputPath}")
                               .Replace(Path.DirectorySeparatorChar, '\\'));

        private IEnumerable<string> ApplyPathsToTemplatedFilePaths(
            IEnumerable<string> files,
            string restorePath,
            string buildOrPublishFolder,
            string intermediateOutputPath) =>
                files.Select(f => f.Replace("${RestorePath}", restorePath)
                                .Replace("${RuntimeVersion}", RuntimeVersion)
                               .Replace("${PackageVersion}", DefaultPackageVersion)
                               .Replace("${OutputPath}", buildOrPublishFolder)
                               .Replace("${IntermediateOutputPath}", intermediateOutputPath)
                               .Replace('\\', Path.DirectorySeparatorChar));


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
                manifest.RelatedManifests.Select(rm => new ComparableManifest(rm.Identity, rm.Source, rm.ManifestType))
                    .Should()
                    .BeEquivalentTo(expected.RelatedManifests.Select(rm => new ComparableManifest(rm.Identity, rm.Source, rm.ManifestType)));
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
            => Path.Combine(BaselinesFolder, $"{name}{(!string.IsNullOrEmpty(suffix) ? $"_{suffix}" : "")}.{manifestType}.staticwebassets.json");

        private string GetExpectedFilesPath(string suffix, string name, string manifestType)
            => Path.Combine(BaselinesFolder, $"{name}{(!string.IsNullOrEmpty(suffix) ? $"_{suffix}" : "")}.{manifestType}.files.json");


        private void ApplyPathsToAssets(
            StaticWebAssetsManifest manifest,
            string projectRoot,
            string restorePath)
        {
            foreach (var asset in manifest.Assets)
            {
                asset.Identity = asset.Identity.Replace("${ProjectRoot}", projectRoot).Replace('\\', Path.DirectorySeparatorChar);
                asset.Identity = asset.Identity
                    .Replace("${RestorePath}", restorePath)
                    .Replace("${RuntimeVersion}", RuntimeVersion)
                    .Replace("${PackageVersion}", DefaultPackageVersion)
                    .Replace('\\', Path.DirectorySeparatorChar);

                asset.ContentRoot = asset.ContentRoot.Replace("${ProjectRoot}", projectRoot).Replace('\\', Path.DirectorySeparatorChar);
                asset.ContentRoot = asset.ContentRoot
                    .Replace("${RestorePath}", restorePath)
                    .Replace("${RuntimeVersion}", RuntimeVersion)
                    .Replace("${PackageVersion}", DefaultPackageVersion)
                    .Replace('\\', Path.DirectorySeparatorChar);
            }

            foreach (var discovery in manifest.DiscoveryPatterns)
            {
                discovery.ContentRoot = discovery.ContentRoot
                    .Replace("${RestorePath}", restorePath)
                    .Replace("${RuntimeVersion}", RuntimeVersion)
                    .Replace("${PackageVersion}", DefaultPackageVersion)
                    .Replace('\\', Path.DirectorySeparatorChar);
                discovery.ContentRoot = discovery.ContentRoot.Replace("${ProjectRoot}", restorePath).Replace('\\', Path.DirectorySeparatorChar);
            }

            foreach (var relatedManifest in manifest.RelatedManifests)
            {
                relatedManifest.Identity = relatedManifest.Identity.Replace("${ProjectRoot}", projectRoot).Replace('\\', Path.DirectorySeparatorChar);
            }
        }

        private string Templatize(StaticWebAssetsManifest manifest, string projectRoot, string restorePath)
        {
            foreach (var asset in manifest.Assets)
            {
                asset.Identity = asset.Identity.Replace(projectRoot, "${ProjectRoot}").Replace(Path.DirectorySeparatorChar, '\\');
                asset.Identity = asset.Identity
                    .Replace(restorePath, "${RestorePath}")
                    .Replace(RuntimeVersion, "${RuntimeVersion}")
                    .Replace(DefaultPackageVersion,"${PackageVersion}")
                    .Replace(Path.DirectorySeparatorChar, '\\');

                asset.ContentRoot = asset.ContentRoot.Replace(projectRoot, "${ProjectRoot}").Replace(Path.DirectorySeparatorChar, '\\');
                asset.ContentRoot = asset.ContentRoot
                    .Replace(restorePath, "${RestorePath}")
                    .Replace(RuntimeVersion, "${RuntimeVersion}")
                    .Replace(DefaultPackageVersion, "${PackageVersion}")
                    .Replace(Path.DirectorySeparatorChar, '\\');
            }

            foreach (var discovery in manifest.DiscoveryPatterns)
            {
                discovery.ContentRoot = discovery.ContentRoot.Replace(projectRoot, "${ProjectRoot}").Replace(Path.DirectorySeparatorChar, '\\');
                discovery.ContentRoot = discovery.ContentRoot
                    .Replace(restorePath, "${RestorePath}")
                    .Replace(RuntimeVersion, "${RuntimeVersion}")
                    .Replace(DefaultPackageVersion, "${PackageVersion}")
                    .Replace(Path.DirectorySeparatorChar, '\\');
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
