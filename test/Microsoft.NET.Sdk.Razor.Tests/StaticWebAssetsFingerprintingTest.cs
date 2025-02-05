// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using System.Text.Json;

namespace Microsoft.NET.Sdk.Razor.Tests;

public class StaticWebAssetsContentFingerprintingIntegrationTest(ITestOutputHelper log) : AspNetSdkBaselineTest(log)
{
    [Fact]
    public void Build_FingerprintsContent_WhenEnabled()
    {
        var expectedManifest = LoadBuildManifest();
        var testAsset = "RazorComponentApp";
        ProjectDirectory = CreateAspNetSdkTestAsset(testAsset)
            .WithProjectChanges(p => {
                var fingerprintContent = p.Descendants()
                    .SingleOrDefault(e => e.Name.LocalName == "StaticWebAssetsFingerprintContent");
                fingerprintContent.Value = "true";
            });

        Directory.CreateDirectory(Path.Combine(ProjectDirectory.Path, "wwwroot", "css"));
        File.WriteAllText(Path.Combine(ProjectDirectory.Path, "wwwroot", "css", "fingerprint-site.css"), "body { color: red; }");

        var build = CreateBuildCommand(ProjectDirectory);
        ExecuteCommand(build).Should().Pass();

        var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
        var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

        // GenerateStaticWebAssetsManifest should generate the manifest file.
        var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
        new FileInfo(path).Should().Exist();
        var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
        AssertManifest(manifest, expectedManifest);

        // GenerateStaticWebAssetsManifest should copy the file to the output folder.
        var finalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.runtime.json");
        new FileInfo(finalPath).Should().Exist();

        var manifest1 = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json")));
        AssertManifest(manifest1, expectedManifest);
        AssertBuildAssets(manifest1, outputPath, intermediateOutputPath);
    }

    [Fact]
    public void Build_WriteImportMapToHtml()
    {
        var testAsset = "VanillaWasm";
        ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

        var build = CreateBuildCommand(ProjectDirectory);
        ExecuteCommand(build).Should().Pass();

        var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
        var indexHtmlPath = Directory.EnumerateFiles(Path.Combine(intermediateOutputPath, "importmaphtml", "build"), "*.html").Single();
        var endpointsManifestPath = Path.Combine(intermediateOutputPath, $"staticwebassets.build.endpoints.json");

        AssertImportMapInHtml(indexHtmlPath, endpointsManifestPath);
    }

    [Fact]
    public void Publish_WriteImportMapToHtml()
    {
        var testAsset = "VanillaWasm";
        ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

        var publish = CreatePublishCommand(ProjectDirectory);
        ExecuteCommand(publish).Should().Pass();

        var outputPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();
        var indexHtmlPath = Path.Combine(outputPath, "wwwroot", "index.html");
        var endpointsManifestPath = Path.Combine(outputPath, $"{testAsset}.staticwebassets.endpoints.json");

        AssertImportMapInHtml(indexHtmlPath, endpointsManifestPath);
    }

    private void AssertImportMapInHtml(string indexHtmlPath, string endpointsManifestPath)
    {
        var indexHtmlContent = File.ReadAllText(indexHtmlPath);
        var endpoints = JsonSerializer.Deserialize<StaticWebAssetEndpointsManifest>(File.ReadAllText(endpointsManifestPath));

        var mainJs = GetFingerprintedPath("main.js");
        Assert.DoesNotContain("src=\"main.js\"", indexHtmlContent);
        Assert.Contains($"src=\"{mainJs}\"", indexHtmlContent);

        Assert.Contains(GetFingerprintedPath("_framework/dotnet.js"), indexHtmlContent);
        Assert.Contains(GetFingerprintedPath("_framework/dotnet.native.js"), indexHtmlContent);
        Assert.Contains(GetFingerprintedPath("_framework/dotnet.runtime.js"), indexHtmlContent);

        string GetFingerprintedPath(string route)
            => endpoints.Endpoints.FirstOrDefault(e => e.Route == route && e.Selectors.Length == 0)?.AssetFile ?? throw new Exception($"Missing endpoint for file '{route}'");
    }
}
