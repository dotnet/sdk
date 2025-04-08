using Microsoft.Build.Utilities;
using Microsoft.DotNet.UnifiedBuild.Tasks;
using Microsoft.DotNet.UnifiedBuild.Tasks.ManifestAssets;
using NuGet.ContentModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;
using static System.Net.Mime.MediaTypeNames;

namespace Microsoft.DotNet.Tests
{
    /// <summary>
    /// Tests for the MergeAssetManifests task. This task is used to join the repo builds in a single vertical
    /// </summary>
    [Trait("Category", "MergeAssetManifests")]
    public class MergeAssetManifestsTests
    {
        private const string manifestsBasePath = "MergeAssetManifestsTests";

        [Theory]
        [InlineData("manifests1")]
        [InlineData("manifests2")]
        [InlineData("manifests3")]
        public static void MergeManifestCheck(string manifestSet)
        {
            // Load all manifests in the manifests2 directory
            var manifestPaths = Directory.EnumerateFiles(AssetsLoader.GetAssetFullPath(Path.Combine(manifestsBasePath, manifestSet)), "*.xml")
                .Where(p => Path.GetFileName(p) != "expected.xml")
                .Select(p => Path.Combine(manifestsBasePath, manifestSet, p))
                .ToList();
            string expectedPath = AssetsLoader.GetAssetFullPath(Path.Combine(manifestsBasePath, manifestSet, "expected.xml"));

            // Create a temporary file to write the merged manifest

            string tempFile = Path.Combine(Path.GetTempFileName());

            try
            {
                var task = new MergeAssetManifests
                {
                    AssetManifest = manifestPaths.Select(p => new TaskItem(AssetsLoader.GetAssetFullPath(p))).ToArray(),
                    VerticalName = "Windows_x64",
                    MergedAssetManifestOutputPath = tempFile,
                    BuildEngine = new MockBuildEngine()
                };

                task.Execute();

                Assert.False(task.Log.HasLoggedErrors);

                // Load the merged manifest xml and compare to the expected output.
                var mergedManifest = XDocument.Load(tempFile);
                var expectedManifest = XDocument.Load(AssetsLoader.GetAssetFullPath(expectedPath));
                Assert.Equal(expectedManifest.ToString(), mergedManifest.ToString());
            }
            finally
            {
                // Clean up the temporary file
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
    }
}
