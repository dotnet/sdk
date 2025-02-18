// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.DotNet.UnifiedBuild.Tasks.ManifestAssets;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Tests
{
    [Trait("Category", "JoinVerticals")]
    public class JoinVerticalsTests
    {
        private ITestOutputHelper OutputHelper { get; }

        public JoinVerticalsTests(ITestOutputHelper outputHelper)
        {
            OutputHelper = outputHelper;
        }

        [Fact]
        public void LoadVerticalManifestTest()
        {
            // act
            var manifest = LoadVerticalManifest("Windows_x64");

            // assert
            Assert.NotNull(manifest);
            Assert.Equal("dotnet-dotnet", manifest.GetAttribute("Name"));
            Assert.Equal("Windows_x64", manifest.GetAttribute("VerticalName"));
        }

        [Fact]
        public void JoinVerticalsExampleTest()
        {
            // prepare
            HashSet<string> verticalNames = [
                "Windows_x64", "Windows_x86", "Windows_x64_BuildPass2",
                "AzureLinux_x64_Cross_x64", "AzureLinux_x64_Cross_Alpine_x64",
                "OSX_x64", "OSX_arm64", ];
            var manifests = verticalNames.Select(LoadVerticalManifest);

            JoinVerticalsConfig joinVerticalsConfig = new JoinVerticalsConfig
            {
                PriorityVerticals = [
                    "Windows_x64_BuildPass2",
                    "Windows_x64",
                    "AzureLinux_x64_Cross_x64",
                    "OSX_arm64",
                ]
            };
            JoinVerticalsAssetSelector joinVerticalsAssetSelector = new JoinVerticalsAssetSelector(joinVerticalsConfig);

            // act
            var selectedVerticals = joinVerticalsAssetSelector.SelectAssetMatchingVertical(manifests).ToList();

            // assert
            PrintTestSelectionResult(selectedVerticals, verticalNames);
            Assert.NotNull(selectedVerticals);
            Assert.Equal(verticalNames.Count(), selectedVerticals.GroupBy(o => o.VerticalName).Count());
            Assert.Contains(selectedVerticals, o => o.MatchType == AssetVerticalMatchType.ExactMatch);
            Assert.Contains(selectedVerticals, o => o.MatchType == AssetVerticalMatchType.PriorityVerticals);
            Assert.DoesNotContain(selectedVerticals, o => o.MatchType == AssetVerticalMatchType.NotSpecified);
        }

        [Fact]
        public void JoinVerticalsCheckAllAmbiguitiesTest()
        {
            // prepare
            var verticals = GetAllVerticalNames();
            var manifests = verticals.Select(LoadVerticalManifest).ToList();
            JoinVerticalsAssetSelector joinVerticalsAssetSelector = new JoinVerticalsAssetSelector();

            // act
            var selectedVerticals = joinVerticalsAssetSelector.SelectAssetMatchingVertical(manifests).ToList();

            // assert
            PrintTestSelectionResult(selectedVerticals, verticals);
            Assert.DoesNotContain(selectedVerticals, o => o.MatchType == AssetVerticalMatchType.NotSpecified);
        }

        [Fact]
        public void GenerateMergedManifestTest()
        {
            // prepare
            var verticals = GetAllVerticalNames();
            var manifests = verticals.Select(LoadVerticalManifest).ToList();
            JoinVerticalsAssetSelector joinVerticalsAssetSelector = new JoinVerticalsAssetSelector();

            // act
            List<AssetVerticalMatchResult> selectedVerticals = joinVerticalsAssetSelector.SelectAssetMatchingVertical(manifests).ToList();

            XDocument mergedManifest = JoinVerticalsManifestExportHelper.ExportMergedManifest(manifests.Single(o => o.VerticalName == "Windows_x64"), selectedVerticals);

            // assert
            Assert.Equal(selectedVerticals.Count(o => o.Asset.AssetType == ManifestAssetType.Package), mergedManifest.Root!.Elements("Package")!.Count());
            Assert.Equal(selectedVerticals.Count(o => o.Asset.AssetType == ManifestAssetType.Blob), mergedManifest.Root!.Elements("Blob")!.Count());
        }

        #region Manifests loading and helper methods

        private const string cVerticalsManifestsPath = "JoinVerticalsTests/manifests/verticals";

        private HashSet<string> GetAllVerticalNames()
        {
            string manifestVirtualDirectoryFullPath = AssetsLoader.GetAssetFullPath(cVerticalsManifestsPath);
            var files = Directory.GetFiles(manifestVirtualDirectoryFullPath, "*.xml");
            return files.Select(file => Path.GetFileNameWithoutExtension(file)).ToHashSet();
        }

        private BuildAssetsManifest LoadVerticalManifest(string verticalName)
        {
            string assetFilePath = Path.Combine(cVerticalsManifestsPath, verticalName + ".xml");
            string manifestFullPath = AssetsLoader.GetAssetFullPath(assetFilePath);
            return BuildAssetsManifest.LoadFromFile(manifestFullPath);
        }

        private void PrintTestSelectionResult(IReadOnlyCollection<AssetVerticalMatchResult> selectedVerticals, HashSet<string>? verticals = null)
        {
            // Enrich the list of verticals with the ones used in the selection if not complete hashset is provided
            if (verticals == null)
            {
                verticals = new HashSet<string>();
            }
            var usedVerticals = selectedVerticals.SelectMany(o => o.OtherVerticals?.Append(o.VerticalName) ?? [o.VerticalName]);
            foreach (var vertical in usedVerticals)
            {
                verticals.Add(vertical);
            }

            // Print assets with unspecified verticals
            var notSpecifiedAssetVerticals = selectedVerticals.Where(o => o.MatchType == AssetVerticalMatchType.NotSpecified).ToList();
            if (notSpecifiedAssetVerticals.Any())
            {
                OutputHelper.WriteLine($"Assets with unspecified vertical:");
                foreach (var assetSelectionInfo in notSpecifiedAssetVerticals)
                {
                    OutputHelper.WriteLine($"{assetSelectionInfo.AssetId}: [{string.Join(", ", assetSelectionInfo.OtherVerticals!)}]");
                }
            }
            else
            {
                OutputHelper.WriteLine("All assets matched to verticals properly");
            }


            // Print verticals usage
            var verticalsUsage = verticals
                .Select(vertical => (vertical, count: selectedVerticals.Count(o => o.VerticalName == vertical)))
                .ToList();

            OutputHelper.WriteLine(string.Empty);
            OutputHelper.WriteLine("Vertical assets count:");
            foreach (var verticalCount in verticalsUsage.Where(o => o.count > 0).OrderByDescending(o => o.count))
            {
                OutputHelper.WriteLine($"{verticalCount.vertical} [{verticalCount.count}]");
            }

            var unusedVerticals = verticalsUsage.Where(o => o.count == 0).ToList();
            if (unusedVerticals.Any())
            {
                OutputHelper.WriteLine(string.Empty);
                OutputHelper.WriteLine("Unused verticals:");
                foreach (var verticalCount in verticalsUsage.Where(o => o.count == 0))
                {
                    OutputHelper.WriteLine($"{verticalCount.vertical}");
                }
            }


            // Ambiguous verticals stats
            var ambiguousAssetsByVertical = selectedVerticals
                .Where(o => o.MatchType == AssetVerticalMatchType.PriorityVerticals)
                .GroupBy(o => o.VerticalName)
                .OrderByDescending(o => o.Count())
                .ToList();
            if (ambiguousAssetsByVertical.Any())
            {
                foreach (var verticalAssets in ambiguousAssetsByVertical)
                {
                    if (StringComparer.OrdinalIgnoreCase.Equals(verticalAssets.Key, "Windows_x64"))
                    {
                        continue;
                    }

                    OutputHelper.WriteLine(string.Empty);
                    OutputHelper.WriteLine($"Ambiguous assets stats for vertical: {verticalAssets.Key}");
                    var otherVerticalGroups = verticalAssets
                        .SelectMany(a => a.OtherVerticals)
                        .GroupBy(v => v)
                        .Select(g => new { Vertical = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count);

                    foreach (var group in otherVerticalGroups)
                    {
                        OutputHelper.WriteLine($" - {group.Vertical}: {group.Count}");
                    }
                    OutputHelper.WriteLine($"Ambiguous assets:");
                    foreach (var assetSelectionInfo in verticalAssets)
                    {
                        OutputHelper.WriteLine($"{assetSelectionInfo.AssetId}");
                    }
                }
            }
        }

        #endregion
    }
}
