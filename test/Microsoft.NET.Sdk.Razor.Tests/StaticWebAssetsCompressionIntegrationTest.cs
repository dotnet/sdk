// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class StaticWebAssetsCompressionIntegrationTest : AspNetSdkBaselineTest
    {
        public StaticWebAssetsCompressionIntegrationTest(ITestOutputHelper log) : base(log, GenerateBaselines) { }

        [Fact]
        public void Build_Detects_PrecompressedAssets()
        {
            var expectedManifest = LoadBuildManifest();
            var testAsset = "RazorAppWithP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var file = Path.Combine(ProjectDirectory.Path, "ClassLibrary", "wwwroot", "js", "project-transitive-dep.js");
            var gzipFile = Path.Combine(ProjectDirectory.Path, "ClassLibrary", "wwwroot", "js", "project-transitive-dep.js.gz");
            var brotliFile = Path.Combine(ProjectDirectory.Path, "ClassLibrary", "wwwroot", "js", "project-transitive-dep.js.br");

            // Compress file into gzip and brotli
            using (var gzipStream = new GZipStream(File.Create(gzipFile), CompressionLevel.NoCompression))
            {
                using var stream = File.OpenRead(file);
                stream.CopyTo(gzipStream);
            }

            using (var brotliStream = new BrotliStream(File.Create(brotliFile), CompressionLevel.NoCompression))
            {
                using var stream = File.OpenRead(file);
                stream.CopyTo(brotliStream);
            }

            var build = CreateBuildCommand(ProjectDirectory, "AppWithP2PReference");
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, expectedManifest);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "AppWithP2PReference.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();

            var manifest1 = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json")));
            AssertManifest(manifest1, expectedManifest);
            AssertBuildAssets(manifest1, outputPath, intermediateOutputPath);
        }
    }
}
