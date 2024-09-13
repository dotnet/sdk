// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.NET.Sdk.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.Razor.Tests;

public partial class StaticWebAssetEndpointsIntegrationTest(ITestOutputHelper log)
    : AspNetSdkBaselineTest(log, GenerateBaselines)
{
    [GeneratedRegex("""(?'project'[a-zA-Z0-9]+)(?:\.(?'fingerprint'[a-zA-Z0-9]*))?\.bundle\.scp\.css(?'compress'\.(?:gz|br))?$""")]
    private static partial Regex ProjectBundleRegex();

    [GeneratedRegex("""(?'project'[a-zA-Z0-9]+)(?:\.(?'fingerprint'[a-zA-Z0-9]*))?\.styles\.css(?'compress'\.(?:gz|br))?$""")]
    private static partial Regex AppBundleRegex();

    [Fact]
    public void Build_CreatesEndpointsForAssets()
    {
        ProjectDirectory = CreateAspNetSdkTestAsset("RazorComponentApp");
        var root = ProjectDirectory.TestRoot;

        var dir = Directory.CreateDirectory(Path.Combine(root, "wwwroot"));
        File.WriteAllText(Path.Combine(dir.FullName, "app.js"), "console.log('hello world!');");

        var build = CreateBuildCommand(ProjectDirectory);
        ExecuteCommand(build).Should().Pass();

        var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
        var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

        // GenerateStaticWebAssetsManifest should generate the manifest file.
        var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
        new FileInfo(path).Should().Exist();
        var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));

        var endpoints = manifest.Endpoints;
        endpoints.Should().HaveCount(15);
        var appJsEndpoints = endpoints.Where(ep => ep.Route.EndsWith("app.js"));
        appJsEndpoints.Should().HaveCount(2);
        var appJsGzEndpoints = endpoints.Where(ep => ep.Route.EndsWith("app.js.gz"));
        appJsGzEndpoints.Should().HaveCount(1);

        // project bundle endpoints
        var bundleEndpoints = endpoints.Where(MatchUncompresedProjectBundlesNoFingerprint);
        bundleEndpoints.Should().HaveCount(2);

        var bundleGzEndpoints = endpoints.Where(MatchCompressedProjectBundlesNoFingerprint);
        bundleGzEndpoints.Should().HaveCount(1);

        var fingerprintedBundleGzEndpoints = endpoints.Where(MatchCompressedProjectBundlesWithFingerprint);
        fingerprintedBundleGzEndpoints.Should().HaveCount(1);

        var fingerprintedBundles = endpoints.Where(MatchUncompressedProjectBundlesWithFingerprint);
        fingerprintedBundles.Should().HaveCount(2);

        // app bundle endpoints
        var appBundleEndpoints = endpoints.Where(MatchUncompressedAppBundleNoFingerprint);
        appBundleEndpoints.Should().HaveCount(2);

        var appBundleGzEndpoints = endpoints.Where(MatchCompressedAppBundleNoFingerprint);
        appBundleGzEndpoints.Should().HaveCount(1);

        var fingerprintedAppBundle = endpoints.Where(MatchUncompressedAppBundleWithFingerprint);
        fingerprintedAppBundle.Should().HaveCount(2);

        var fingerprintedAppBundleGz = endpoints.Where(MatchCompressedAppBundleWithFingerprint);
        fingerprintedAppBundleGz.Should().HaveCount(1);

        AssertManifest(manifest, LoadBuildManifest());
    }

    private bool MatchUncompresedProjectBundlesNoFingerprint(StaticWebAssetEndpoint ep) => ProjectBundleRegex().Match(ep.Route) is
    {
        Success: true,
        Groups: [
            var _,
            { Name: "project", Value: "ComponentApp", Success: true, },
            { Name: "fingerprint", Value: "", Success: false },
            { Name: "compress", Value: "", Success: false }
        ]
    };

    private bool MatchCompressedProjectBundlesNoFingerprint(StaticWebAssetEndpoint ep) => ProjectBundleRegex().Match(ep.Route) is
    {
        Success: true,
        Groups: [
            var _,
            { Name: "project", Value: "ComponentApp", Success: true, },
            { Name: "fingerprint", Value: "", Success: false },
            { Name: "compress", Value: var compress, Success: true }
        ]
    } && (compress == ".gz" || compress == ".br");

    private bool MatchUncompressedProjectBundlesWithFingerprint(StaticWebAssetEndpoint ep) => ProjectBundleRegex().Match(ep.Route) is
    {
        Success: true,
        Groups: [
            var m,
            { Name: "project", Value: "ComponentApp", Success: true, },
            { Name: "fingerprint", Value: var fingerprint, Success: true },
            { Name: "compress", Value: "", Success: false }
        ]
    } && fingerprint == ep.EndpointProperties.Single(p => p.Name == "fingerprint").Value;

    private bool MatchCompressedProjectBundlesWithFingerprint(StaticWebAssetEndpoint ep) => ProjectBundleRegex().Match(ep.Route) is
    {
        Success: true,
        Groups: [
            var m,
            { Name: "project", Value: "ComponentApp", Success: true, },
            { Name: "fingerprint", Value: var fingerprint, Success: true },
            { Name: "compress", Value: var compress, Success: true }
        ]
    } && !string.IsNullOrWhiteSpace(fingerprint)
      && (compress == ".gz" || compress == ".br");

    private bool MatchUncompressedAppBundleNoFingerprint(StaticWebAssetEndpoint ep) => AppBundleRegex().Match(ep.Route) is
    {
        Success: true,
        Groups: [
            var _,
            { Name: "project", Value: "ComponentApp", Success: true, },
            { Name: "fingerprint", Value: "", Success: false },
            { Name: "compress", Value: "", Success: false }
        ]
    };

    private bool MatchCompressedAppBundleNoFingerprint(StaticWebAssetEndpoint ep) => AppBundleRegex().Match(ep.Route) is
    {
        Success: true,
        Groups: [
            var _,
            { Name: "project", Value: "ComponentApp", Success: true, },
            { Name: "fingerprint", Value: "", Success: false },
            { Name: "compress", Value: var compress, Success: true }
        ]
    } && (compress == ".gz" || compress == ".br");

    private bool MatchUncompressedAppBundleWithFingerprint(StaticWebAssetEndpoint ep) => AppBundleRegex().Match(ep.Route) is
    {
        Success: true,
        Groups: [
            var m,
            { Name: "project", Value: "ComponentApp", Success: true, },
            { Name: "fingerprint", Value: var fingerprint, Success: true },
            { Name: "compress", Value: "", Success: false }
        ]
    } && fingerprint == ep.EndpointProperties.Single(p => p.Name == "fingerprint").Value;

    private bool MatchCompressedAppBundleWithFingerprint(StaticWebAssetEndpoint ep) => AppBundleRegex().Match(ep.Route) is
    {
        Success: true,
        Groups: [
            var m,
            { Name: "project", Value: "ComponentApp", Success: true, },
            { Name: "fingerprint", Value: var fingerprint, Success: true },
            { Name: "compress", Value: var compress, Success: true }
        ]
    } && !string.IsNullOrWhiteSpace(fingerprint)
      && (compress == ".gz" || compress == ".br");

    [Fact]
    public void Publish_CreatesEndpointsForAssets()
    {
        ProjectDirectory = CreateAspNetSdkTestAsset("RazorComponentApp");
        var root = ProjectDirectory.TestRoot;

        var dir = Directory.CreateDirectory(Path.Combine(root, "wwwroot"));
        File.WriteAllText(Path.Combine(dir.FullName, "app.js"), "console.log('hello world!');");

        var publish = CreatePublishCommand(ProjectDirectory);
        ExecuteCommand(publish).Should().Pass();

        var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
        var outputPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

        // GenerateStaticWebAssetsManifest should generate the manifest file.
        var path = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
        new FileInfo(path).Should().Exist();
        var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));

        var endpoints = manifest.Endpoints;

        foreach (var endpoint in endpoints)
        {
            var contentLength = endpoint.ResponseHeaders.Single(rh => rh.Name == "Content-Length");
            var length = long.Parse(contentLength.Value, CultureInfo.InvariantCulture);
            var file = new FileInfo(endpoint.AssetFile);
            file.Should().Exist();
            file.Length.Should().Be(length, $"because {endpoint.Route} {file.FullName}");
        }

        endpoints.Should().HaveCount(25);
        var appJsEndpoints = endpoints.Where(ep => ep.Route.EndsWith("app.js"));
        appJsEndpoints.Should().HaveCount(3);
        var appJsGzEndpoints = endpoints.Where(ep => ep.Route.EndsWith("app.js.gz"));
        appJsGzEndpoints.Should().HaveCount(1);
        var appJsBrEndpoints = endpoints.Where(ep => ep.Route.EndsWith("app.js.br"));
        appJsBrEndpoints.Should().HaveCount(1);

        var uncompressedAppJsEndpoint = appJsEndpoints.Where(ep => ep.Selectors.Length == 0);
        uncompressedAppJsEndpoint.Should().HaveCount(1);
        uncompressedAppJsEndpoint.Single().ResponseHeaders.Select(h => h.Name).Should().BeEquivalentTo(
            [
                "Accept-Ranges",
                "Cache-Control",
                "Content-Length",
                "Content-Type",
                "ETag",
                "Last-Modified"
            ]
        );

        var eTagHeader = uncompressedAppJsEndpoint.Single().ResponseHeaders.Single(h => h.Name == "ETag");

        var gzipCompressedAppJsEndpoint = appJsEndpoints.Where(ep => ep.Selectors.Length == 1 && ep.Selectors[0].Value == "gzip");
        gzipCompressedAppJsEndpoint.Should().HaveCount(1);
        gzipCompressedAppJsEndpoint.Single().ResponseHeaders.Select(h => h.Name).Should().BeEquivalentTo(
            [
                "Accept-Ranges",
                "Cache-Control",
                "Content-Length",
                "Content-Type",
                "ETag",
                "Last-Modified",
                "Content-Encoding",
                "Vary",
                "ETag"
            ]
        );
        gzipCompressedAppJsEndpoint.Single().ResponseHeaders.Any(h => h.Name == "ETag" && h.Value.StartsWith("W/")).Should().BeTrue();
        var gzipWeakEtag = gzipCompressedAppJsEndpoint.Single().ResponseHeaders.Single(h => h.Name == "ETag" && h.Value.StartsWith("W/")).Value;
        gzipWeakEtag[2..].Should().Be(eTagHeader.Value);

        var brotliCompressedAppJsEndpoint = appJsEndpoints.Where(ep => ep.Selectors.Length == 1 && ep.Selectors[0].Value == "br");
        brotliCompressedAppJsEndpoint.Should().HaveCount(1);
        brotliCompressedAppJsEndpoint.Single().ResponseHeaders.Select(h => h.Name).Should().BeEquivalentTo(
            [
                "Accept-Ranges",
                "Cache-Control",
                "Content-Length",
                "Content-Type",
                "ETag",
                "Last-Modified",
                "Content-Encoding",
                "Vary",
                "ETag",
            ]
        );
        brotliCompressedAppJsEndpoint.Single().ResponseHeaders.Any(h => h.Name == "ETag" && h.Value.StartsWith("W/")).Should().BeTrue();
        var brWeakEtag = brotliCompressedAppJsEndpoint.Single().ResponseHeaders.Single(h => h.Name == "ETag" && h.Value.StartsWith("W/")).Value;
        brWeakEtag[2..].Should().Be(eTagHeader.Value);

        var bundleEndpoints = endpoints.Where(MatchUncompresedProjectBundlesNoFingerprint);
        bundleEndpoints.Should().HaveCount(3);
        var bundleGzEndpoints = endpoints.Where(MatchCompressedProjectBundlesNoFingerprint).Where(ep => ep.Route.EndsWith(".gz"));
        bundleGzEndpoints.Should().HaveCount(1);
        var bundleBrEndpoints = endpoints.Where(MatchCompressedProjectBundlesNoFingerprint).Where(ep => ep.Route.EndsWith(".br"));
        bundleBrEndpoints.Should().HaveCount(1);
        var fingerprintedBundleGzEndpoints = endpoints.Where(MatchCompressedProjectBundlesWithFingerprint).Where(ep => ep.Route.EndsWith(".gz"));
        fingerprintedBundleGzEndpoints.Should().HaveCount(1);
        var fingerprintedBundleBrEndpoints = endpoints.Where(MatchCompressedProjectBundlesWithFingerprint).Where(ep => ep.Route.EndsWith(".br"));
        fingerprintedBundleBrEndpoints.Should().HaveCount(1);

        var fingerprintedBundleEndpoints = endpoints.Where(MatchUncompressedProjectBundlesWithFingerprint);
        fingerprintedBundleEndpoints.Should().HaveCount(3);

        var appBundleEndpoints = endpoints.Where(MatchUncompressedAppBundleNoFingerprint);
        appBundleEndpoints.Should().HaveCount(3);
        var appBundleGzEndpoints = endpoints.Where(MatchCompressedAppBundleNoFingerprint).Where(ep => ep.Route.EndsWith(".gz"));
        appBundleGzEndpoints.Should().HaveCount(1);
        var appBundleBrEndpoints = endpoints.Where(MatchCompressedAppBundleNoFingerprint).Where(ep => ep.Route.EndsWith(".br"));
        appBundleBrEndpoints.Should().HaveCount(1);
        var fingerprintedAppBundleGzEndpoints = endpoints.Where(MatchCompressedAppBundleWithFingerprint).Where(ep => ep.Route.EndsWith(".gz"));
        fingerprintedAppBundleGzEndpoints.Should().HaveCount(1);
        var fingerprintedAppBundleBrEndpoints = endpoints.Where(MatchCompressedAppBundleWithFingerprint).Where(ep => ep.Route.EndsWith(".br"));
        fingerprintedAppBundleBrEndpoints.Should().HaveCount(1);

        var fingerprintedAppBundleEndpoints = endpoints.Where(MatchUncompressedAppBundleWithFingerprint);
        fingerprintedAppBundleEndpoints.Should().HaveCount(3);

        AssertManifest(manifest, LoadPublishManifest());
    }

    [Fact]
    public void Publish_CreatesEndpointsForAssets_BuildAndPublish_Assets()
    {
        ProjectDirectory = CreateAspNetSdkTestAsset("RazorComponentApp")
            .WithProjectChanges(document =>
            {
                document.Root.AddFirst(
                    new XElement("ItemGroup",
                        new XElement("Content",
                            new XAttribute("Update", "wwwroot/app.js"),
                            new XAttribute("CopyToPublishDirectory", "Never")),
                        new XElement("Content",
                            new XAttribute("Update", "wwwroot/app.publish.js"),
                            new XAttribute("TargetPath", "wwwroot/app.js"),
                            new XAttribute("CopyToPublishDirectory", "PreserveNewest"))));
                var doc2 = document;
            });
        var root = ProjectDirectory.TestRoot;

        var dir = Directory.CreateDirectory(Path.Combine(root, "wwwroot"));
        File.WriteAllText(Path.Combine(dir.FullName, "app.js"), "console.log('hello world!');");
        File.WriteAllText(Path.Combine(dir.FullName, "app.publish.js"), "console.log('publish hello world!');");

        var publish = CreatePublishCommand(ProjectDirectory);
        ExecuteCommand(publish).Should().Pass();

        var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
        var publishOutputPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

        // GenerateStaticWebAssetsManifest should generate the manifest file.
        var path = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
        new FileInfo(path).Should().Exist();
        var buildManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
        AssertManifest(buildManifest, LoadPublishManifest());

        var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.publish.json")));

        var endpoints = publishManifest.Endpoints;

        var appJsEndpoints = endpoints.Where(ep => ep.Route.EndsWith("app.js"));
        appJsEndpoints.Should().HaveCount(3);

        // There's only 1 uncompressed asset endpoint.
        var unCompressedAssetEndpoint = appJsEndpoints.Where(ep => ep.Selectors.Length == 0);
        unCompressedAssetEndpoint.Should().HaveCount(1);

        // The uncompressed asset endpoint is for the publish asset.
        var publishAsset = publishManifest.Assets.Where(a => a.Identity == unCompressedAssetEndpoint.Single().AssetFile);
        publishAsset.Should().HaveCount(1);

        // There is only 1 gzip asset endpoint.
        var appGzAssetEndpoint = appJsEndpoints.Where(ep => ep.Selectors.Length == 1 && ep.Selectors[0].Value == "gzip");
        appGzAssetEndpoint.Should().HaveCount(1);

        // The gzip asset endpoint is for the gzip compressed version of the publish asset.
        var publishGzAsset = publishManifest.Assets.Where(a => a.Identity == appGzAssetEndpoint.Single().AssetFile);
        publishGzAsset.Should().HaveCount(1);
        publishGzAsset.Single().RelatedAsset.Should().Be(publishAsset.Single().Identity);

        // There is only 1 br asset endpoint.
        var appBrAssetEndpoint = appJsEndpoints.Where(ep => ep.Selectors.Length == 1 && ep.Selectors[0].Value == "br");
        appBrAssetEndpoint.Should().HaveCount(1);

        // The br asset endpoint is for the br compressed version of the publish asset.
        var publishBrAsset = publishManifest.Assets.Where(a => a.Identity == appBrAssetEndpoint.Single().AssetFile);
        publishBrAsset.Should().HaveCount(1);
        publishBrAsset.Single().RelatedAsset.Should().Be(publishAsset.Single().Identity);

        // The compressed gzip and br assets are exposed with their extensions.
        var appJsGzEndpoints = endpoints.Where(ep => ep.Route.EndsWith("app.js.gz"));
        appJsGzEndpoints.Should().HaveCount(1);

        var appJsBrEndpoints = endpoints.Where(ep => ep.Route.EndsWith("app.js.br"));
        appJsBrEndpoints.Should().HaveCount(1);

        var bundleEndpoints = endpoints.Where(MatchUncompresedProjectBundlesNoFingerprint);
        bundleEndpoints.Should().HaveCount(3);
        var bundleGzEndpoints = endpoints.Where(MatchCompressedProjectBundlesNoFingerprint).Where(ep => ep.Route.EndsWith(".gz"));
        bundleGzEndpoints.Should().HaveCount(1);
        var bundleBrEndpoints = endpoints.Where(MatchCompressedProjectBundlesNoFingerprint).Where(ep => ep.Route.EndsWith(".br"));
        bundleBrEndpoints.Should().HaveCount(1);
        var fingerprintedBundleGzEndpoints = endpoints.Where(MatchCompressedProjectBundlesWithFingerprint).Where(ep => ep.Route.EndsWith(".gz"));
        fingerprintedBundleGzEndpoints.Should().HaveCount(1);
        var fingerprintedBundleBrEndpoints = endpoints.Where(MatchCompressedProjectBundlesWithFingerprint).Where(ep => ep.Route.EndsWith(".br"));
        fingerprintedBundleBrEndpoints.Should().HaveCount(1);

        var fingerprintedBundleEndpoints = endpoints.Where(MatchUncompressedProjectBundlesWithFingerprint);
        fingerprintedBundleEndpoints.Should().HaveCount(3);

        var appBundleEndpoints = endpoints.Where(MatchUncompressedAppBundleNoFingerprint);
        appBundleEndpoints.Should().HaveCount(3);
        var appBundleGzEndpoints = endpoints.Where(MatchCompressedAppBundleNoFingerprint).Where(ep => ep.Route.EndsWith(".gz"));
        appBundleGzEndpoints.Should().HaveCount(1);
        var appBundleBrEndpoints = endpoints.Where(MatchCompressedAppBundleNoFingerprint).Where(ep => ep.Route.EndsWith(".br"));
        appBundleBrEndpoints.Should().HaveCount(1);
        var fingerprintedAppBundleGzEndpoints = endpoints.Where(MatchCompressedAppBundleWithFingerprint).Where(ep => ep.Route.EndsWith(".gz"));
        fingerprintedAppBundleGzEndpoints.Should().HaveCount(1);
        var fingerprintedAppBundleBrEndpoints = endpoints.Where(MatchCompressedAppBundleWithFingerprint).Where(ep => ep.Route.EndsWith(".br"));
        fingerprintedAppBundleBrEndpoints.Should().HaveCount(1);

        var fingerprintedAppBundleEndpoints = endpoints.Where(MatchUncompressedAppBundleWithFingerprint);
        fingerprintedAppBundleEndpoints.Should().HaveCount(3);

        endpoints.Should().HaveCount(25);

        AssertManifest(publishManifest, LoadPublishManifest());
    }

    [Fact]
    public void Build_EndpointManifest_ContainsEndpoints()
    {
        // Arrange
        var expectedExtensions = new[] { ".pdb", ".js", ".wasm" };
        var testAppName = "BlazorWasmWithLibrary";
        var testInstance = CreateAspNetSdkTestAsset(testAppName)
            .WithProjectChanges((p, doc) =>
            {
                if (Path.GetFileName(p) == "blazorwasm.csproj")
                {
                    var itemGroup = new XElement("PropertyGroup");
                    var serviceWorkerAssetsManifest = new XElement("ServiceWorkerAssetsManifest", "service-worker-assets.js");
                    var fingerprintAssets = new XElement("WasmFingerprintAssets", false);
                    itemGroup.Add(serviceWorkerAssetsManifest);
                    itemGroup.Add(fingerprintAssets);
                    doc.Root.Add(itemGroup);
                }
            });

        var buildCommand = CreateBuildCommand(testInstance, "blazorwasm");
        buildCommand.Execute("/bl").Should().Pass();

        var buildOutputDirectory = buildCommand.GetOutputDirectory(DefaultTfm).ToString();
        VerifyEndpointsCollection(buildOutputDirectory, "blazorwasm", readFromDevManifest: true);
    }

    [Fact]
    public void BuildHosted_EndpointManifest_ContainsEndpoints()
    {
        // Arrange
        var testAppName = "BlazorHosted";
        var testInstance = CreateAspNetSdkTestAsset(testAppName)
            .WithProjectChanges((p, doc) =>
                {
                    if (Path.GetFileName(p) == "blazorwasm.csproj")
                    {
                        var itemGroup = new XElement("PropertyGroup");
                        var fingerprintAssets = new XElement("WasmFingerprintAssets", false);
                        itemGroup.Add(fingerprintAssets);
                        doc.Root.Add(itemGroup);
                    }
                });

        var buildCommand = CreateBuildCommand(testInstance, "blazorhosted");
        buildCommand.Execute()
            .Should().Pass();

        var buildOutputDirectory = OutputPathCalculator.FromProject(Path.Combine(testInstance.TestRoot, "blazorhosted")).GetOutputDirectory();

        VerifyEndpointsCollection(buildOutputDirectory, "blazorhosted", readFromDevManifest: true);
    }

    [Fact]
    public void Publish_EndpointManifestContainsEndpoints()
    {
        // Arrange
        var testAppName = "BlazorWasmWithLibrary";
        var testInstance = CreateAspNetSdkTestAsset(testAppName)
            .WithProjectChanges((p, doc) =>
            {
                if (Path.GetFileName(p) == "blazorwasm.csproj")
                {
                    var itemGroup = new XElement("PropertyGroup");
                    var fingerprintAssets = new XElement("WasmFingerprintAssets", false);
                    itemGroup.Add(fingerprintAssets);
                    doc.Root.Add(itemGroup);
                }
            });

        var publishCommand = CreatePublishCommand(testInstance, "blazorwasm");
        publishCommand.Execute().Should().Pass();

        var publishOutputDirectory = publishCommand.GetOutputDirectory(DefaultTfm).ToString();

        VerifyEndpointsCollection(publishOutputDirectory, "blazorwasm");
    }

    [Fact]
    public void PublishHosted_EndpointManifest_ContainsEndpoints()
    {
        // Arrange
        var testAppName = "BlazorHosted";
        var testInstance = CreateAspNetSdkTestAsset(testAppName)
            .WithProjectChanges((p, doc) =>
            {
                if (Path.GetFileName(p) == "blazorwasm.csproj")
                {
                    var itemGroup = new XElement("PropertyGroup");
                    var fingerprintAssets = new XElement("WasmFingerprintAssets", false);
                    itemGroup.Add(fingerprintAssets);
                    doc.Root.Add(itemGroup);
                }
            });

        var publishCommand = CreatePublishCommand(testInstance, "blazorhosted");
        publishCommand.Execute().Should().Pass();

        var publishOutputDirectory = publishCommand.GetOutputDirectory(DefaultTfm).ToString();

        VerifyEndpointsCollection(publishOutputDirectory, "blazorhosted");
    }

    // Makes several assertions about the endpoints we defined.
    // All assets have at least one endpoint.
    // No endpoint points to a non-existent asset
    // All compressed assets have 2 endpoints (one for the path with the extension, one for the path without the extension)
    // All uncompressed assets have 1 endpoint
    private static void VerifyEndpointsCollection(string outputDirectory, string projectName, bool readFromDevManifest = false)
    {
        var endpointsManifestFile = Path.Combine(outputDirectory, $"{projectName}.staticwebassets.endpoints.json");

        var endpoints = JsonSerializer.Deserialize<StaticWebAssetEndpointsManifest>(File.ReadAllText(endpointsManifestFile));

        var wwwrootFolderFiles = GetWwwrootFolderFiles(outputDirectory);

        var foundAssets = new HashSet<string>();
        var endpointsByAssetFile = endpoints.Endpoints.GroupBy(e => e.AssetFile).ToDictionary(g => g.Key, g => g.ToArray());

        foreach (var endpoint in endpoints.Endpoints)
        {
            wwwrootFolderFiles.Should().Contain(endpoint.AssetFile);
            foundAssets.Add(endpoint.AssetFile);
        }

        wwwrootFolderFiles.Should().BeEquivalentTo(foundAssets);

        foreach (var file in wwwrootFolderFiles)
        {
            endpointsByAssetFile.Should().ContainKey(file);
            if (file.EndsWith(".br") || file.EndsWith(".gz"))
            {
                endpointsByAssetFile[file].Should().HaveCount(2);
            }
            else if (endpointsByAssetFile[file].Length > 1)
            {
                endpointsByAssetFile[file].Where(e => e.EndpointProperties.Any(p => p.Name == "integrity")).Count().Should().Be(1);
                endpointsByAssetFile[file].Where(e => e.EndpointProperties.Length == 0).Count().Should().Be(1);
            }
            else
            {
                endpointsByAssetFile[file].Should().HaveCount(1);
            }
        }

        HashSet<string> GetWwwrootFolderFiles(string outputDirectory)
        {
            if (!readFromDevManifest)
            {
                return new(Directory.GetFiles(Path.Combine(outputDirectory, "wwwroot"), "*", SearchOption.AllDirectories)
                        .Select(a => StaticWebAsset.Normalize(Path.GetRelativePath(Path.Combine(outputDirectory, "wwwroot"), a))));
            }
            else
            {
                var staticWebAssetDevelopmentManifest = JsonSerializer.Deserialize<StaticWebAssetsDevelopmentManifest>(File.ReadAllText(Path.Combine(outputDirectory, $"{projectName}.staticwebassets.runtime.json")));
                var endpoints = new HashSet<string>();

                //Traverse the node tree and compute the paths for all assets
                Traverse(staticWebAssetDevelopmentManifest.Root, "", endpoints);
                return endpoints;
            }
        }
    }

    private static void Traverse(StaticWebAssetNode node, string pathSoFar, HashSet<string> endpoints)
    {
        if (node.Asset != null)
        {
            endpoints.Add(StaticWebAsset.Normalize(pathSoFar));
        }
        else
        {
            foreach (var child in node.Children)
            {
                Traverse(child.Value, Path.Combine(pathSoFar, child.Key), endpoints);
            }
        }
    }

    public class StaticWebAssetsDevelopmentManifest
    {
        public string[] ContentRoots { get; set; }

        public StaticWebAssetNode Root { get; set; }
    }

    public class StaticWebAssetPattern
    {
        public int ContentRootIndex { get; set; }
        public string Pattern { get; set; }
        public int Depth { get; set; }
    }

    public class StaticWebAssetMatch
    {
        public int ContentRootIndex { get; set; }
        public string SubPath { get; set; }
    }

    public class StaticWebAssetNode
    {
        public Dictionary<string, StaticWebAssetNode> Children { get; set; }
        public StaticWebAssetMatch Asset { get; set; }
        public StaticWebAssetPattern[] Patterns { get; set; }
    }
}
