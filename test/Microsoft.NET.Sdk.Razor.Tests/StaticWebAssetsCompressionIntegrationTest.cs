// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;
using System.Net.Http.Headers;
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

            var manifest2 = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json")));

            var standardEndpoints = manifest2.Endpoints.Where(e => string.Equals(e.AssetFile, file, StringComparison.Ordinal)).ToArray();
            var gzipEndpoints = manifest2.Endpoints.Where(e => string.Equals(e.AssetFile, gzipFile, StringComparison.Ordinal)).ToArray();
            var brotliEndpoints = manifest2.Endpoints.Where(e => string.Equals(e.AssetFile, brotliFile, StringComparison.Ordinal)).ToArray();

            var gzipAsset = manifest2.Assets.Single(a => string.Equals(a.Identity, gzipFile, StringComparison.Ordinal));
            var brotliAsset = manifest2.Assets.Single(a => string.Equals(a.Identity, brotliFile, StringComparison.Ordinal));

            standardEndpoints.Should().HaveCount(1);
            gzipEndpoints.Should().HaveCount(2);
            brotliEndpoints.Should().HaveCount(2);

            var expectedWeakEndpointEtag = new EntityTagHeaderValue(
                EntityTagHeaderValue.Parse(standardEndpoints.First().ResponseHeaders.Single(h => h.Name == "ETag").Value).Tag,
                isWeak: true);

            foreach (var endpoint in gzipEndpoints)
            {
                endpoint.ResponseHeaders.Where(e => e.Name == "Content-Encoding").Select(e => e.Value).Single().Should().Be("gzip");

                var etags = endpoint.ResponseHeaders.Where(e => e.Name == "ETag").Select(e => EntityTagHeaderValue.Parse(e.Value));
                etags.Where(e=> !e.IsWeak).Select(e => e.Tag).Single().Should().BeEquivalentTo($"\"{gzipAsset.Integrity}\"");
                if (endpoint.Route.EndsWith(".gz"))
                {
                    continue;
                }
                etags.Should().Contain(expectedWeakEndpointEtag);
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
                etags.Should().Contain(expectedWeakEndpointEtag);
            }
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

            var expectedWeakEndpointEtag = new EntityTagHeaderValue(
                EntityTagHeaderValue.Parse(standardEndpoints.First().ResponseHeaders.Single(h => h.Name == "ETag").Value).Tag,
                isWeak: true);

            foreach (var endpoint in gzipEndpoints)
            {
                endpoint.ResponseHeaders.Where(e => e.Name == "Content-Encoding").Select(e => e.Value).Single().Should().Be("gzip");

                var etags = endpoint.ResponseHeaders.Where(e => e.Name == "ETag").Select(e => EntityTagHeaderValue.Parse(e.Value));
                etags.Where(e => !e.IsWeak).Select(e => e.Tag).Single().Should().BeEquivalentTo($"\"{gzipAsset.Integrity}\"");
                if (endpoint.Route.EndsWith(".gz"))
                {
                    continue;
                }
                etags.Should().Contain(expectedWeakEndpointEtag);
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
                etags.Should().Contain(expectedWeakEndpointEtag);
            }
        }
    }
}
