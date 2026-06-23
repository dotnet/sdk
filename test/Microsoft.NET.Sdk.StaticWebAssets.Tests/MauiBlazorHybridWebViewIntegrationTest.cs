// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Json;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests
{
    // Regression coverage for https://github.com/dotnet/sdk/issues/54779.
    //
    // A MAUI Blazor Hybrid app references a Razor class library AND the BlazorWebView package. The
    // package ships a fallback `blazor.modules.json` that should only be used when the app does not
    // generate its own JS modules manifest.
    //
    // The idiomatic authoring (mirroring Microsoft.AspNetCore.Components.WebAssembly, and tracked for
    // BlazorWebView by dotnet/aspnetcore#67374) ships `blazor.modules.json` as a Framework asset: it
    // is materialized once into the consuming project at build, becomes an ordinary project asset, and
    // the app's own generated manifest wins naturally — with the package manifest serving as the
    // fallback when the app has no JS modules. The test asset encodes that behavior; these tests track
    // both outcomes (app manifest wins / package fallback used) so the scenario keeps working.
    //
    // The previous authoring instead promoted the SDK-generated manifest to AssetKind=All and relied on
    // deferred group resolution to drop the fallback; deferred groups are skipped at publish, leaving
    // two `All` manifests on `_framework/blazor.modules.json` and throwing
    // "Sequence contains more than one element".
    public class MauiBlazorHybridWebViewIntegrationTest(ITestOutputHelper log)
        : IsolatedNuGetPackageFolderAspNetSdkBaselineTest(log, nameof(MauiBlazorHybridWebViewIntegrationTest))
    {
        private const string ModulesManifestRoute = "_framework/blazor.modules.json";

        [Fact]
        public void Publish_FrameworkModulesAsset_AppHasModules_AppManifestWins()
        {
            // The app contributes its own JS library modules, so the SDK generates a blazor.modules.json.
            // The package's materialized framework manifest is superseded: publish succeeds and exactly
            // one endpoint (the app's own generated manifest) is produced for the route.
            var result = PublishMauiHybridApp(
                packageVersion: "1.0.0",
                out var intermediateOutputPath,
                out var publishOutputPath);

            result.Should().Pass();

            var moduleManifestEndpoints = LoadPublishEndpoints(intermediateOutputPath)
                .Where(e => e.Route == ModulesManifestRoute)
                .ToList();

            moduleManifestEndpoints.Should().ContainSingle(
                "the app's generated blazor.modules.json should be the only manifest for the route");

            // The app's own generated manifest (SourceType=Computed) should be present and win.
            AssetsAtTargetPath(intermediateOutputPath, ModulesManifestRoute)
                .Should().Contain(a => a.SourceType == "Computed",
                    "the app's own generated manifest should win over the package fallback");

            LoadPublishedModulesManifest(publishOutputPath).Should().BeEquivalentTo(
                [
                    "MauiBlazorApp.lib.module.js",
                    "_content/MauiRazorClassLibrary/MauiRazorClassLibrary.lib.module.js"
                ],
                "the generated app manifest should include the app and referenced RCL JS modules");
        }

        [Fact]
        public void Publish_FrameworkModulesAsset_NoAppModules_UsesMaterializedPackageFallback()
        {
            // When the app (and its referenced libraries) have no JS library modules, no manifest is
            // generated, so the materialized package fallback is the manifest that is served.
            var result = PublishMauiHybridApp(
                packageVersion: "2.0.0",
                out var intermediateOutputPath,
                out var publishOutputPath,
                removeAppJsModules: true);

            result.Should().Pass();

            var moduleManifestEndpoints = LoadPublishEndpoints(intermediateOutputPath)
                .Where(e => e.Route == ModulesManifestRoute)
                .ToList();

            moduleManifestEndpoints.Should().ContainSingle(
                "the materialized package fallback should be the only manifest for the route");

            // The package fallback was materialized into the consuming project (SourceType=Discovered),
            // and there is no app-generated manifest.
            AssetsAtTargetPath(intermediateOutputPath, ModulesManifestRoute)
                .Should().OnlyContain(a => a.SourceType == "Discovered",
                    "only the materialized package fallback should remain when the app has no JS modules");

            LoadPublishedModulesManifest(publishOutputPath).Should().BeEmpty(
                "the package fallback manifest content is an empty modules list");
        }

        private CommandResult PublishMauiHybridApp(
            string packageVersion,
            out string intermediateOutputPath,
            out string publishOutputPath,
            bool removeAppJsModules = false)
        {
            ProjectDirectory = CreateAspNetSdkTestAsset("MauiBlazorHybridWebView", identifier: packageVersion);

            if (removeAppJsModules)
            {
                File.Delete(Path.Combine(ProjectDirectory.TestRoot, "MauiBlazorApp", "wwwroot", "MauiBlazorApp.lib.module.js"));
                File.Delete(Path.Combine(ProjectDirectory.TestRoot, "MauiRazorClassLibrary", "wwwroot", "MauiRazorClassLibrary.lib.module.js"));
            }

            var properties = new[]
            {
                $"-p:BlazorWebViewPackageVersion={packageVersion}",
            };

            var pack = CreatePackCommand(ProjectDirectory, "BlazorWebViewPackage");
            ExecuteCommand(pack, properties).Should().Pass();

            var restore = CreateRestoreCommand(ProjectDirectory, "MauiBlazorApp");
            ExecuteCommand(restore, properties).Should().Pass();

            var publish = CreatePublishCommand(ProjectDirectory, "MauiBlazorApp");
            var result = ExecuteCommand(publish, properties);

            intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            publishOutputPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();
            return result;
        }

        private static List<StaticWebAsset> AssetsAtTargetPath(string intermediateOutputPath, string targetPath)
        {
            var manifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(manifestPath));
            return manifest.Assets
                .Where(a => a.ComputeTargetPath("", '/', StaticWebAssetTokenResolver.Instance) == targetPath)
                .ToList();
        }

        private static StaticWebAssetEndpoint[] LoadPublishEndpoints(string intermediateOutputPath)
        {
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.publish.endpoints.json");
            var manifest = JsonSerializer.Deserialize<StaticWebAssetEndpointsManifest>(
                File.ReadAllBytes(path),
                StaticWebAssetsJsonSerializerContext.Default.StaticWebAssetEndpointsManifest);
            return manifest?.Endpoints ?? [];
        }

        private static string[] LoadPublishedModulesManifest(string publishOutputPath)
        {
            var modulesManifestPath = Path.Combine(
                publishOutputPath,
                "wwwroot",
                ModulesManifestRoute.Replace('/', Path.DirectorySeparatorChar));
            new FileInfo(modulesManifestPath).Should().Exist("publish should produce {0}", ModulesManifestRoute);
            return JsonSerializer.Deserialize<string[]>(File.ReadAllBytes(modulesManifestPath)) ?? [];
        }
    }
}
