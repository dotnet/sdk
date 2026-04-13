// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests
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

            var manifest2 = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json")));

            var standardEndpoints = manifest2.Endpoints.Where(e => string.Equals(e.AssetFile, file, StringComparison.Ordinal)).ToArray();
            var gzipEndpoints = manifest2.Endpoints.Where(e => string.Equals(e.AssetFile, gzipFile, StringComparison.Ordinal)).ToArray();
            var brotliEndpoints = manifest2.Endpoints.Where(e => string.Equals(e.AssetFile, brotliFile, StringComparison.Ordinal)).ToArray();

            var gzipAsset = manifest2.Assets.Single(a => string.Equals(a.Identity, gzipFile, StringComparison.Ordinal));
            var brotliAsset = manifest2.Assets.Single(a => string.Equals(a.Identity, brotliFile, StringComparison.Ordinal));

            standardEndpoints.Should().HaveCount(1);
            gzipEndpoints.Should().HaveCount(2);
            brotliEndpoints.Should().HaveCount(2);


            foreach (var endpoint in gzipEndpoints)
            {
                endpoint.ResponseHeaders.Where(e => e.Name == "Content-Encoding").Select(e => e.Value).Single().Should().Be("gzip");

                var etags = endpoint.ResponseHeaders.Where(e => e.Name == "ETag").Select(e => EntityTagHeaderValue.Parse(e.Value));
                etags.Where(e=> !e.IsWeak).Select(e => e.Tag).Single().Should().BeEquivalentTo($"\"{gzipAsset.Integrity}\"");
                if (endpoint.Route.EndsWith(".gz"))
                {
                    continue;
                }
            }

            foreach (var endpoint in brotliEndpoints)
            {
                endpoint.ResponseHeaders.Where(e => e.Name == "Content-Encoding").Select(e => e.Value).Single().Should().Be("br");

                var etags = endpoint.ResponseHeaders.Where(e => e.Name == "ETag").Select(e => EntityTagHeaderValue.Parse(e.Value));
                etags.Where(e => !e.IsWeak).Select(e => e.Tag).Single().Should().BeEquivalentTo($"\"{brotliAsset.Integrity}\"");
                if (endpoint.Route.EndsWith(".br"))
                {
                    continue;
                }
            }
        }

        [Fact]
        public void CanEnable_CompressionOnAllAssets()
        {
            var expectedManifest = LoadBuildManifest();
            var testAsset = "RazorAppWithP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset)
                .WithProjectChanges((project, xml) =>
                {
                    if (project.Contains("ClassLibrary"))
                    {
                        xml.Descendants("PropertyGroup")
                            .First().Add(new XElement("StaticWebAssetBuildCompressAllAssets", "true"));
                    }
                });

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
        }

        [Fact]
        public void PublishWorks_With_PrecompressedAssets()
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

            var build = CreatePublishCommand(ProjectDirectory, "AppWithP2PReference");
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, expectedManifest);


            var manifest1 = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json")));
            AssertManifest(manifest1, expectedManifest);
            AssertBuildAssets(manifest1, outputPath, intermediateOutputPath);

            var manifest2 = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.publish.json")));

            var standardEndpoints = manifest2.Endpoints.Where(e => string.Equals(e.AssetFile, file, StringComparison.Ordinal)).ToArray();
            var gzipEndpoints = manifest2.Endpoints.Where(e => string.Equals(e.AssetFile, gzipFile, StringComparison.Ordinal)).ToArray();
            var brotliEndpoints = manifest2.Endpoints.Where(e => string.Equals(e.AssetFile, brotliFile, StringComparison.Ordinal)).ToArray();

            var gzipAsset = manifest2.Assets.Single(a => string.Equals(a.Identity, gzipFile, StringComparison.Ordinal));
            var brotliAsset = manifest2.Assets.Single(a => string.Equals(a.Identity, brotliFile, StringComparison.Ordinal));

            standardEndpoints.Should().HaveCount(1);
            gzipEndpoints.Should().HaveCount(2);
            brotliEndpoints.Should().HaveCount(2);


            foreach (var endpoint in gzipEndpoints)
            {
                endpoint.ResponseHeaders.Where(e => e.Name == "Content-Encoding").Select(e => e.Value).Single().Should().Be("gzip");

                var etags = endpoint.ResponseHeaders.Where(e => e.Name == "ETag").Select(e => EntityTagHeaderValue.Parse(e.Value));
                etags.Where(e => !e.IsWeak).Select(e => e.Tag).Single().Should().BeEquivalentTo($"\"{gzipAsset.Integrity}\"");
                if (endpoint.Route.EndsWith(".gz"))
                {
                    continue;
                }
            }

            foreach (var endpoint in brotliEndpoints)
            {
                endpoint.ResponseHeaders.Where(e => e.Name == "Content-Encoding").Select(e => e.Value).Single().Should().Be("br");

                var etags = endpoint.ResponseHeaders.Where(e => e.Name == "ETag").Select(e => EntityTagHeaderValue.Parse(e.Value));
                etags.Where(e => !e.IsWeak).Select(e => e.Tag).Single().Should().BeEquivalentTo($"\"{brotliAsset.Integrity}\"");
                if (endpoint.Route.EndsWith(".br"))
                {
                    continue;
                }
            }
        }

        [Fact]
        public void Publish_WithPreviousPack_GeneratesDczForModifiedAssets()
        {
            var testAsset = "RazorAppWithP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset)
                .WithProjectChanges((project, xml) =>
                {
                    if (project.Contains("AppWithP2PReference"))
                    {
                        var propGroup = xml.Descendants("PropertyGroup").First();
                        propGroup.Add(new XElement("StaticWebAssetDictionaryCompression", "true"));
                    }
                });

            // === First publish: generates an asset pack, no dcz (no previous pack) ===
            var publish1 = CreatePublishCommand(ProjectDirectory, "AppWithP2PReference");
            ExecuteCommand(publish1).Should().Pass();

            var outputPath1 = publish1.GetOutputDirectory(DefaultTfm, "Debug").ToString();
            var intermediateOutputPath1 = publish1.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            // The asset pack is at $(OutputPath)staticwebassets.pack.zip, which is the
            // bin/Debug/tfm/ folder (parent of the publish subfolder).
            var outputDir = Path.GetDirectoryName(outputPath1.TrimEnd(Path.DirectorySeparatorChar))!;
            var packPath = Path.Combine(outputDir, "staticwebassets.pack.zip");
            new FileInfo(packPath).Should().Exist("because GeneratePublishAssetPack should create the pack on first publish");

            // Read the v1 publish manifest to verify no dcz endpoints exist
            var v1ManifestPath = Path.Combine(intermediateOutputPath1, "staticwebassets.publish.json");
            var v1Manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(v1ManifestPath));
            v1Manifest.Endpoints
                .Where(e => e.Selectors != null && e.Selectors.Any(s => s.Name == "Content-Encoding" && s.Value == "dcz"))
                .Should().BeEmpty("because no previous pack was provided for the first publish");

            // Save the pack to project root (outside bin/obj)
            var savedPackPath = Path.Combine(ProjectDirectory.Path, "previous.pack.zip");
            File.Copy(packPath, savedPackPath, overwrite: true);

            // === Clean bin/obj for the app project ===
            var appDir = Path.Combine(ProjectDirectory.Path, "AppWithP2PReference");
            var binDir = Path.Combine(appDir, "bin");
            var objDir = Path.Combine(appDir, "obj");
            if (Directory.Exists(binDir))
            {
                Directory.Delete(binDir, true);
            }
            if (Directory.Exists(objDir))
            {
                Directory.Delete(objDir, true);
            }

            // === Modify one of the ClassLibrary wwwroot files ===
            var fileToModify = Path.Combine(ProjectDirectory.Path, "ClassLibrary", "wwwroot", "js", "project-transitive-dep.js");
            File.AppendAllText(fileToModify, "\n// modified for v2\n");

            // === Second publish: with previous pack, should produce dcz for modified file ===
            var publish2 = CreatePublishCommand(ProjectDirectory, "AppWithP2PReference");
            ExecuteCommand(publish2,
                $"/p:StaticWebAssetPreviousAssetPack={savedPackPath}")
                .Should().Pass();

            var intermediateOutputPath2 = publish2.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            // Read the v2 publish manifest
            var v2ManifestPath = Path.Combine(intermediateOutputPath2, "staticwebassets.publish.json");
            var v2Manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(v2ManifestPath));

            // Find dcz endpoints — should exist only for the modified file
            var dczEndpoints = v2Manifest.Endpoints
                .Where(e => e.Selectors != null && e.Selectors.Any(s => s.Name == "Content-Encoding" && s.Value == "dcz"))
                .ToArray();

            // The modified file should have dcz endpoints (2: one for the original route, one for the .dcz route)
            dczEndpoints.Should().NotBeEmpty("because the modified file should have delta-compressed variants");

            // All dcz endpoints should reference the modified file's route
            var dczEndpointsForModifiedFile = dczEndpoints
                .Where(e => e.Route.Contains("project-transitive-dep"))
                .ToArray();
            dczEndpointsForModifiedFile.Should().HaveCountGreaterThanOrEqualTo(1,
                "because the modified file should have dcz endpoints");

            // dcz endpoints should have Available-Dictionary selector
            foreach (var dczEndpoint in dczEndpointsForModifiedFile)
            {
                var dczSelector = dczEndpoint.Selectors
                    .Where(s => s.Name == "Content-Encoding" && s.Value == "dcz")
                    .ToArray();
                dczSelector.Should().NotBeEmpty("because dcz endpoints need a Content-Encoding=dcz selector");

                var dictSelector = dczEndpoint.Selectors
                    .Where(s => s.Name == "Available-Dictionary")
                    .ToArray();
                dictSelector.Should().NotBeEmpty("because dcz endpoints need an Available-Dictionary selector");
            }

            // The unmodified file (project-transitive-dep.v4.js) should NOT have dcz endpoints
            // because its content hasn't changed and same-hash candidates are skipped
            var unchangedDczEndpoints = dczEndpoints
                .Where(e => e.Route.Contains("project-transitive-dep.v4"))
                .ToArray();
            unchangedDczEndpoints.Should().BeEmpty(
                "because unchanged files should not get dcz endpoints (same integrity means dictionary is pointless)");

            // Verify dcz assets exist on disk
            var dczAssets = v2Manifest.Assets
                .Where(a => string.Equals(a.AssetTraitName, "Content-Encoding", StringComparison.Ordinal)
                    && string.Equals(a.AssetTraitValue, "dcz", StringComparison.Ordinal))
                .ToArray();
            dczAssets.Should().NotBeEmpty("because dcz compressed files should be generated for modified assets");
            foreach (var dczAsset in dczAssets)
            {
                new FileInfo(dczAsset.Identity).Should().Exist($"because dcz asset '{dczAsset.Identity}' should exist on disk");
            }

            // Verify the original (uncompressed) endpoint for the modified file has Use-As-Dictionary header
            var originalEndpoints = v2Manifest.Endpoints
                .Where(e => e.Route.Contains("project-transitive-dep.js")
                    && !e.Route.Contains(".v4")
                    && (e.Selectors == null || !e.Selectors.Any(s => s.Name == "Content-Encoding")))
                .ToArray();
            var useDictHeaders = originalEndpoints
                .SelectMany(e => e.ResponseHeaders ?? Enumerable.Empty<StaticWebAssetEndpointResponseHeader>())
                .Where(h => h.Name == "Use-As-Dictionary")
                .ToArray();
            useDictHeaders.Should().NotBeEmpty(
                "because the original asset endpoint should have a Use-As-Dictionary response header");
        }
    }
}
