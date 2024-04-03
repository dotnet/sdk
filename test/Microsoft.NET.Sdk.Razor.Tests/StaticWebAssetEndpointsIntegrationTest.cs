// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.NET.Sdk.StaticWebAssets.Tasks;
using NuGet.Packaging;

namespace Microsoft.NET.Sdk.Razor.Tests;

public class StaticWebAssetEndpointsIntegrationTest(ITestOutputHelper log)
    : AspNetSdkBaselineTest(log, GenerateBaselines)
{
    [Fact]
    public void Build_CreatesEndpointsForAssets()
    {
        ProjectDirectory = CreateAspNetSdkTestAsset("RazorComponentApp");
        var root = ProjectDirectory.TestRoot;

        var dir = Directory.CreateDirectory(Path.Combine(root, "wwwroot"));
        File.WriteAllText(Path.Combine(dir.FullName, "app.js"), "console.log('hello world!');");

        var build = new BuildCommand(ProjectDirectory);
        build.WithWorkingDirectory(ProjectDirectory.TestRoot);
        build.Execute("/bl").Should().Pass();

        var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
        var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

        // GenerateStaticWebAssetsManifest should generate the manifest file.
        var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
        new FileInfo(path).Should().Exist();
        var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));

        var endpoints = manifest.Endpoints;
        endpoints.Should().HaveCount(9);
        var appJsEndpoints = endpoints.Where(ep => ep.Route.EndsWith("app.js"));
        appJsEndpoints.Should().HaveCount(2);
        var appJsGzEndpoints = endpoints.Where(ep => ep.Route.EndsWith("app.js.gz"));
        appJsGzEndpoints.Should().HaveCount(1);

        var bundleEndpoints = endpoints.Where(ep => ep.Route.EndsWith("bundle.scp.css"));
        bundleEndpoints.Should().HaveCount(2);
        var bundleGzEndpoints = endpoints.Where(ep => ep.Route.EndsWith("bundle.scp.css.gz"));
        bundleGzEndpoints.Should().HaveCount(1);

        var appBundleEndpoints = endpoints.Where(ep => ep.Route.EndsWith("ComponentApp.styles.css"));
        appBundleEndpoints.Should().HaveCount(2);
        var appBundleGzEndpoints = endpoints.Where(ep => ep.Route.EndsWith("ComponentApp.styles.css.gz"));
        appBundleGzEndpoints.Should().HaveCount(1);

        AssertManifest(manifest, LoadBuildManifest());
    }

    [Fact]
    public void Publish_CreatesEndpointsForAssets()
    {
        ProjectDirectory = CreateAspNetSdkTestAsset("RazorComponentApp");
        var root = ProjectDirectory.TestRoot;

        var dir = Directory.CreateDirectory(Path.Combine(root, "wwwroot"));
        File.WriteAllText(Path.Combine(dir.FullName, "app.js"), "console.log('hello world!');");

        var publish = new PublishCommand(ProjectDirectory);
        publish.WithWorkingDirectory(ProjectDirectory.TestRoot);
        publish.Execute("/bl").Should().Pass();

        var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
        var outputPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

        // GenerateStaticWebAssetsManifest should generate the manifest file.
        var path = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
        new FileInfo(path).Should().Exist();
        var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));

        var endpoints = manifest.Endpoints;
        endpoints.Should().HaveCount(15);
        var appJsEndpoints = endpoints.Where(ep => ep.Route.EndsWith("app.js"));
        appJsEndpoints.Should().HaveCount(3);
        var appJsGzEndpoints = endpoints.Where(ep => ep.Route.EndsWith("app.js.gz"));
        appJsGzEndpoints.Should().HaveCount(1);
        var appJsBrEndpoints = endpoints.Where(ep => ep.Route.EndsWith("app.js.br"));
        appJsBrEndpoints.Should().HaveCount(1);

        var bundleEndpoints = endpoints.Where(ep => ep.Route.EndsWith("bundle.scp.css"));
        bundleEndpoints.Should().HaveCount(3);
        var bundleGzEndpoints = endpoints.Where(ep => ep.Route.EndsWith("bundle.scp.css.gz"));
        bundleGzEndpoints.Should().HaveCount(1);
        var bundleBrEndpoints = endpoints.Where(ep => ep.Route.EndsWith("bundle.scp.css.br"));
        bundleBrEndpoints.Should().HaveCount(1);

        var appBundleEndpoints = endpoints.Where(ep => ep.Route.EndsWith("ComponentApp.styles.css"));
        appBundleEndpoints.Should().HaveCount(3);
        var appBundleGzEndpoints = endpoints.Where(ep => ep.Route.EndsWith("ComponentApp.styles.css.gz"));
        appBundleGzEndpoints.Should().HaveCount(1);
        var appBundleBrEndpoints = endpoints.Where(ep => ep.Route.EndsWith("ComponentApp.styles.css.br"));
        appBundleBrEndpoints.Should().HaveCount(1);

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

        var publish = new PublishCommand(ProjectDirectory);
        publish.WithWorkingDirectory(ProjectDirectory.TestRoot);
        publish.Execute("/bl").Should().Pass();

        var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
        var publishOutputPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

        // GenerateStaticWebAssetsManifest should generate the manifest file.
        var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
        new FileInfo(path).Should().Exist();
        var buildManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
        AssertManifest(buildManifest, LoadBuildManifest());

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

        var bundleEndpoints = endpoints.Where(ep => ep.Route.EndsWith("bundle.scp.css"));
        bundleEndpoints.Should().HaveCount(3);
        var bundleGzEndpoints = endpoints.Where(ep => ep.Route.EndsWith("bundle.scp.css.gz"));
        bundleGzEndpoints.Should().HaveCount(1);
        var bundleBrEndpoints = endpoints.Where(ep => ep.Route.EndsWith("bundle.scp.css.br"));
        bundleBrEndpoints.Should().HaveCount(1);

        var appBundleEndpoints = endpoints.Where(ep => ep.Route.EndsWith("ComponentApp.styles.css"));
        appBundleEndpoints.Should().HaveCount(3);
        var appBundleGzEndpoints = endpoints.Where(ep => ep.Route.EndsWith("ComponentApp.styles.css.gz"));
        appBundleGzEndpoints.Should().HaveCount(1);
        var appBundleBrEndpoints = endpoints.Where(ep => ep.Route.EndsWith("ComponentApp.styles.css.br"));
        appBundleBrEndpoints.Should().HaveCount(1);

        endpoints.Should().HaveCount(15);

        AssertManifest(publishManifest, LoadPublishManifest());
    }
}
