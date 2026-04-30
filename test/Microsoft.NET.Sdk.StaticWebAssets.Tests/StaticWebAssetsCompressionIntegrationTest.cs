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

            // === Collect all endpoints for the modified file across both manifests ===
            var modifiedRoute = "_content/ClassLibrary/js/project-transitive-dep.js";
            Func<StaticWebAssetEndpoint, bool> modifiedFileRouteFilter = e =>
                e.Route.Contains("project-transitive-dep") && !e.Route.Contains(".v4");

            var v1EndpointsForFile = v1Manifest.Endpoints.Where(modifiedFileRouteFilter).ToArray();
            var v2EndpointsForFile = v2Manifest.Endpoints.Where(modifiedFileRouteFilter).ToArray();

            // Helper to get the encoding for an endpoint (null means identity/uncompressed)
            static string? GetEncoding(StaticWebAssetEndpoint ep) =>
                ep.Selectors?.FirstOrDefault(s => s.Name == "Content-Encoding").Value;

            // Helper to check a header exists with a given name and value
            static bool HasHeader(StaticWebAssetEndpoint ep, string name, string? value = null) =>
                ep.ResponseHeaders.Any(h =>
                    string.Equals(h.Name, name, StringComparison.Ordinal) &&
                    (value == null || string.Equals(h.Value, value, StringComparison.Ordinal)));

            // Helper to get header value
            static string? GetHeaderValue(StaticWebAssetEndpoint ep, string name) =>
                ep.ResponseHeaders.FirstOrDefault(h => string.Equals(h.Name, name, StringComparison.Ordinal)).Value;

            // Helper to check a property exists
            static bool HasProperty(StaticWebAssetEndpoint ep, string name) =>
                ep.EndpointProperties != null && ep.EndpointProperties.Any(p => string.Equals(p.Name, name, StringComparison.Ordinal));

            // === Verify endpoint counts ===
            // v1: 7 endpoints (gzip/br/zstd content-negotiated + identity + .br/.gz/.zst direct)
            // v2: 9 endpoints (above + dcz content-negotiated + .dcz direct)
            v1EndpointsForFile.Should().HaveCount(7, "because v1 should have identity + 3 compressed (gzip/br/zstd) + 3 direct (.br/.gz/.zst)");
            v2EndpointsForFile.Should().HaveCount(9, "because v2 adds dcz content-negotiated + .dcz direct = 7 + 2");

            // === Identify each endpoint in v2 by route + encoding ===
            // Content-negotiated endpoints (all share the same route, distinguished by Content-Encoding selector)
            var cnEndpoints = v2EndpointsForFile.Where(e => e.Route == modifiedRoute).ToArray();
            cnEndpoints.Should().HaveCount(5, "because there should be 5 content-negotiated endpoints: identity, gzip, br, zstd, dcz");

            var cnByEncoding = cnEndpoints.ToDictionary(
                e => GetEncoding(e) ?? "identity",
                e => e);
            cnByEncoding.Keys.Should().BeEquivalentTo(["identity", "gzip", "br", "zstd", "dcz"]);

            // Direct-access endpoints (route ends with the compression extension)
            var directEndpoints = v2EndpointsForFile.Where(e => e.Route != modifiedRoute).ToArray();
            directEndpoints.Should().HaveCount(4, "because there should be 4 direct endpoints: .br, .dcz, .gz, .zst");

            // The dcz direct route includes the old file fingerprint: name.{fingerprint}.dcz
            var dczDirectEndpoint = directEndpoints.Single(e => e.Route.EndsWith(".dcz", StringComparison.Ordinal));
            dczDirectEndpoint.Route.Should().StartWith(modifiedRoute + ".", "dcz route should be derived from the modified route");
            dczDirectEndpoint.Route.Should().EndWith(".dcz");

            var nonDczDirectRoutes = directEndpoints
                .Where(e => !e.Route.EndsWith(".dcz", StringComparison.Ordinal))
                .Select(e => e.Route).Order().ToArray();
            nonDczDirectRoutes.Should().BeEquivalentTo([
                $"{modifiedRoute}.br",
                $"{modifiedRoute}.gz",
                $"{modifiedRoute}.zst"
            ]);
            var directByRoute = directEndpoints.ToDictionary(e => e.Route, e => e);

            // === Get v1 integrity for cross-validation of Available-Dictionary ===
            var v1IdentityEndpoint = v1EndpointsForFile
                .First(e => e.Route == modifiedRoute && GetEncoding(e) == null);
            var v1IntegrityValue = v1IdentityEndpoint.EndpointProperties
                .First(p => p.Name == "integrity").Value;
            // v1 integrity looks like "sha256-G0av3NPyLxQq2kXU9Rwosav2qz6ghu5aTHJ63tsI4I0="
            // Available-Dictionary uses the structured field byte sequence form: ":G0av3NPyLxQq2kXU9Rwosav2qz6ghu5aTHJ63tsI4I0=:"
            var v1HashBase64 = v1IntegrityValue.Substring("sha256-".Length);
            var expectedDictValue = $":{v1HashBase64}:";

            // =====================================================================
            // Verify each content-negotiated endpoint
            // =====================================================================

            // --- identity (uncompressed) endpoint ---
            var identityEp = cnByEncoding["identity"];
            identityEp.Selectors.Should().BeNullOrEmpty("because the identity endpoint has no Content-Encoding selector");
            HasHeader(identityEp, "Cache-Control", "no-cache").Should().BeTrue();
            HasHeader(identityEp, "Content-Length").Should().BeTrue();
            HasHeader(identityEp, "Content-Type", "text/javascript").Should().BeTrue();
            HasHeader(identityEp, "ETag").Should().BeTrue();
            HasHeader(identityEp, "Last-Modified").Should().BeTrue();
            HasHeader(identityEp, "Vary", "Accept-Encoding").Should().BeTrue();
            // CDT-specific: identity endpoint gets Use-As-Dictionary and Vary: Available-Dictionary
            HasHeader(identityEp, "Use-As-Dictionary").Should().BeTrue(
                "because the identity endpoint should advertise dictionary availability");
            GetHeaderValue(identityEp, "Use-As-Dictionary").Should().Contain(
                "match=\"/_content/ClassLibrary/js/project-transitive-dep",
                "because Use-As-Dictionary must specify a match pattern scoped to the resource path");
            HasHeader(identityEp, "Vary", "Available-Dictionary").Should().BeTrue(
                "because the identity endpoint must vary on Available-Dictionary when dcz variants exist");
            HasProperty(identityEp, "integrity").Should().BeTrue();
            // identity endpoint should NOT have Content-Encoding header
            HasHeader(identityEp, "Content-Encoding").Should().BeFalse();

            // --- gzip content-negotiated endpoint ---
            var gzipEp = cnByEncoding["gzip"];
            gzipEp.Selectors.Should().ContainSingle(s => s.Name == "Content-Encoding" && s.Value == "gzip");
            HasHeader(gzipEp, "Cache-Control", "no-cache").Should().BeTrue();
            HasHeader(gzipEp, "Content-Encoding", "gzip").Should().BeTrue();
            HasHeader(gzipEp, "Content-Length").Should().BeTrue();
            HasHeader(gzipEp, "Content-Type", "text/javascript").Should().BeTrue();
            HasHeader(gzipEp, "ETag").Should().BeTrue();
            HasHeader(gzipEp, "Last-Modified").Should().BeTrue();
            HasHeader(gzipEp, "Vary", "Accept-Encoding").Should().BeTrue();
            HasProperty(gzipEp, "integrity").Should().BeTrue();
            HasProperty(gzipEp, "original-resource").Should().BeTrue();
            // Per RFC 9842, all content-negotiated responses should include Use-As-Dictionary
            // so the client stores the decompressed body as a dictionary regardless of encoding
            HasHeader(gzipEp, "Use-As-Dictionary").Should().BeTrue(
                "because the gzip response, once decompressed, can be stored as a dictionary");
            GetHeaderValue(gzipEp, "Use-As-Dictionary").Should().Contain(
                "match=\"/_content/ClassLibrary/js/project-transitive-dep");
            HasHeader(gzipEp, "Vary", "Available-Dictionary").Should().BeTrue(
                "because endpoints with Use-As-Dictionary must vary on Available-Dictionary");

            // --- brotli content-negotiated endpoint ---
            var brEp = cnByEncoding["br"];
            brEp.Selectors.Should().ContainSingle(s => s.Name == "Content-Encoding" && s.Value == "br");
            HasHeader(brEp, "Cache-Control", "no-cache").Should().BeTrue();
            HasHeader(brEp, "Content-Encoding", "br").Should().BeTrue();
            HasHeader(brEp, "Content-Length").Should().BeTrue();
            HasHeader(brEp, "Content-Type", "text/javascript").Should().BeTrue();
            HasHeader(brEp, "ETag").Should().BeTrue();
            HasHeader(brEp, "Last-Modified").Should().BeTrue();
            HasHeader(brEp, "Vary", "Accept-Encoding").Should().BeTrue();
            HasProperty(brEp, "integrity").Should().BeTrue();
            HasProperty(brEp, "original-resource").Should().BeTrue();
            HasHeader(brEp, "Use-As-Dictionary").Should().BeTrue(
                "because the brotli response, once decompressed, can be stored as a dictionary");
            GetHeaderValue(brEp, "Use-As-Dictionary").Should().Contain(
                "match=\"/_content/ClassLibrary/js/project-transitive-dep");
            HasHeader(brEp, "Vary", "Available-Dictionary").Should().BeTrue(
                "because endpoints with Use-As-Dictionary must vary on Available-Dictionary");

            // --- zstd content-negotiated endpoint ---
            var zstdEp = cnByEncoding["zstd"];
            zstdEp.Selectors.Should().ContainSingle(s => s.Name == "Content-Encoding" && s.Value == "zstd");
            HasHeader(zstdEp, "Cache-Control", "no-cache").Should().BeTrue();
            HasHeader(zstdEp, "Content-Encoding", "zstd").Should().BeTrue();
            HasHeader(zstdEp, "Content-Length").Should().BeTrue();
            HasHeader(zstdEp, "Content-Type", "text/javascript").Should().BeTrue();
            HasHeader(zstdEp, "ETag").Should().BeTrue();
            HasHeader(zstdEp, "Last-Modified").Should().BeTrue();
            HasHeader(zstdEp, "Vary", "Accept-Encoding").Should().BeTrue();
            HasProperty(zstdEp, "integrity").Should().BeTrue();
            HasProperty(zstdEp, "original-resource").Should().BeTrue();
            HasHeader(zstdEp, "Use-As-Dictionary").Should().BeTrue(
                "because the zstd response, once decompressed, can be stored as a dictionary");
            GetHeaderValue(zstdEp, "Use-As-Dictionary").Should().Contain(
                "match=\"/_content/ClassLibrary/js/project-transitive-dep");
            HasHeader(zstdEp, "Vary", "Available-Dictionary").Should().BeTrue(
                "because endpoints with Use-As-Dictionary must vary on Available-Dictionary");

            // --- dcz content-negotiated endpoint (CDT-specific) ---
            var dczEp = cnByEncoding["dcz"];
            // Selector: Content-Encoding=dcz; Dictionary-Hash is an endpoint property (not a selector)
            dczEp.Selectors.Should().HaveCount(1);
            dczEp.Selectors.Should().Contain(s => s.Name == "Content-Encoding" && s.Value == "dcz");
            HasProperty(dczEp, "Dictionary-Hash").Should().BeTrue(
                "because dcz endpoints carry the dictionary hash as a property for routing");
            var dictHashProp = dczEp.EndpointProperties.First(p => p.Name == "Dictionary-Hash");
            dictHashProp.Value.Should().Be(expectedDictValue,
                $"because Dictionary-Hash should reference the v1 asset hash ({v1HashBase64})");
            // Headers
            HasHeader(dczEp, "Cache-Control", "no-cache").Should().BeTrue();
            HasHeader(dczEp, "Content-Encoding", "dcz").Should().BeTrue();
            HasHeader(dczEp, "Content-Length").Should().BeTrue();
            HasHeader(dczEp, "Content-Type", "text/javascript").Should().BeTrue();
            HasHeader(dczEp, "ETag").Should().BeTrue();
            HasHeader(dczEp, "Last-Modified").Should().BeTrue();
            HasHeader(dczEp, "Vary", "Accept-Encoding").Should().BeTrue();
            HasHeader(dczEp, "Vary", "Available-Dictionary").Should().BeTrue(
                "because dcz endpoints must vary on Available-Dictionary");
            HasProperty(dczEp, "integrity").Should().BeTrue();
            HasProperty(dczEp, "original-resource").Should().BeTrue();
            // dcz should NOT have Use-As-Dictionary — it serves delta-compressed content,
            // not the full resource body that can be stored as a dictionary
            HasHeader(dczEp, "Use-As-Dictionary").Should().BeFalse();

            // =====================================================================
            // Verify each direct-access endpoint
            // =====================================================================

            // All direct-access endpoints share common properties: no Content-Encoding selector,
            // but they DO have a Content-Encoding response header matching their format.
            foreach (var (route, expectedEncoding) in new[]
            {
                ($"{modifiedRoute}.gz", "gzip"),
                ($"{modifiedRoute}.br", "br"),
                ($"{modifiedRoute}.zst", "zstd"),
                (dczDirectEndpoint.Route, "dcz"),
            })
            {
                var directEp = directByRoute[route];
                (directEp.Selectors == null || directEp.Selectors.Length == 0 ||
                 !directEp.Selectors.Any(s => s.Name == "Content-Encoding"))
                    .Should().BeTrue($"because direct endpoint {route} should not have a Content-Encoding selector");
                HasHeader(directEp, "Cache-Control", "no-cache").Should().BeTrue($"on {route}");
                HasHeader(directEp, "Content-Encoding", expectedEncoding).Should().BeTrue($"on {route}");
                HasHeader(directEp, "Content-Length").Should().BeTrue($"on {route}");
                // dcz direct route includes old fingerprint so Content-Type is derived from .dcz, not .js
                if (expectedEncoding != "dcz")
                {
                    HasHeader(directEp, "Content-Type", "text/javascript").Should().BeTrue($"on {route}");
                }
                else
                {
                    HasHeader(directEp, "Content-Type").Should().BeTrue($"on {route}");
                }
                HasHeader(directEp, "ETag").Should().BeTrue($"on {route}");
                HasHeader(directEp, "Last-Modified").Should().BeTrue($"on {route}");
                HasHeader(directEp, "Vary", "Accept-Encoding").Should().BeTrue($"on {route}");
                HasProperty(directEp, "integrity").Should().BeTrue($"on {route}");
                HasHeader(directEp, "Use-As-Dictionary").Should().BeFalse($"on {route}");
                HasHeader(directEp, "Vary", "Available-Dictionary").Should().BeFalse($"on {route}");
            }

            // =====================================================================
            // Verify dcz assets exist on disk
            // =====================================================================
            var dczAssets = v2Manifest.Assets
                .Where(a => string.Equals(a.AssetTraitName, "Content-Encoding", StringComparison.Ordinal)
                    && string.Equals(a.AssetTraitValue, "dcz", StringComparison.Ordinal))
                .ToArray();
            dczAssets.Should().ContainSingle("because there should be exactly one dcz asset for the modified file");
            new FileInfo(dczAssets[0].Identity).Should().Exist("because the dcz compressed file should exist on disk");

            // =====================================================================
            // Verify the UNCHANGED file has NO dcz endpoints
            // =====================================================================
            var unchangedDczEndpoints = v2Manifest.Endpoints
                .Where(e => e.Route.Contains("project-transitive-dep.v4")
                    && ((e.Selectors != null
                        && e.Selectors.Any(s => s.Name == "Content-Encoding" && s.Value == "dcz"))
                        || e.Route.EndsWith(".dcz", StringComparison.Ordinal)))
                .ToArray();
            unchangedDczEndpoints.Should().BeEmpty(
                "because unchanged files should not get dcz endpoints (same integrity means dictionary is pointless)");

            // Verify unchanged file still has its normal endpoints (identity, gzip, br, zstd)
            var unchangedEndpoints = v2Manifest.Endpoints
                .Where(e => e.Route.Contains("project-transitive-dep.v4"))
                .ToArray();
            unchangedEndpoints.Should().NotBeEmpty(
                "because the unchanged file should still be present in the manifest with its normal endpoints");
            unchangedEndpoints.Should().Contain(e => e.Selectors == null || e.Selectors.Length == 0,
                "because the unchanged file should have an identity endpoint");
        }
    }
}
