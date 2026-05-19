// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using Microsoft.AspNetCore.StaticWebAssets.Tasks;

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
}
