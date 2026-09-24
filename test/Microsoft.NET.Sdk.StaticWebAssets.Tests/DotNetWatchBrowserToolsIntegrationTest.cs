// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests
{
    /// <summary>
    /// The <c>dotnet watch</c> browser tools client, its configuration and the key pair that
    /// authenticates the provider are all produced by the build, never by the provider the browser
    /// authenticates. These tests pin that contract: the key pair is generated into deterministic
    /// intermediate output paths, only its public half is pinned into a static web asset, the
    /// initializer is generated only when the existing Hot Reload build property is enabled, and
    /// none of it reaches publish output.
    /// </summary>
    [TestClass]
    public class DotNetWatchBrowserToolsIntegrationTest : IsolatedNuGetPackageFolderAspNetSdkBaselineTest
    {
        protected override string RestoreNugetPackagePath => nameof(DotNetWatchBrowserToolsIntegrationTest);

        private const string TestAsset = "RazorComponentApp";
        private const string ConfigFileName = "Microsoft.NET.Sdk.BlazorWeb.DotNetWatch.BrowserTools.Config.js";
        private const string ClientFileName = "Microsoft.NET.Sdk.BlazorWeb.DotNetWatch.BrowserTools.js";
        private const string InitializerFileName = "Microsoft.NET.Sdk.BlazorWeb.DotNetWatch.lib.module.js";
        private const string SettingsFileName = "hot-reload-settings.json";
        private const string SettingsBuildMarkerFileName = "hot-reload-settings.build.marker";
        private const string SettingsRoute = "/_framework/dotnet-browser-tools/hot-reload-settings.json";

        // Contract with Microsoft.DotNet.Watch.BrowserToolsBuildOutputs.
        private const string PublicKeyFileName = "browser-tools-key.public.json";
        private const string PrivateKeyFileName = "browser-tools-key.private.json";

        private string GeneratedDirectory(MSBuildCommand command)
            => Path.Combine(command.GetIntermediateDirectory(DefaultTfm, "Debug").ToString(), "dotnet-watch");

        private static bool SettingsRequireBuild(string settingsPath, string markerPath)
            => !File.Exists(settingsPath)
                || !File.Exists(markerPath)
                || File.GetLastWriteTimeUtc(settingsPath) > File.GetLastWriteTimeUtc(markerPath);

        private void AssertDesignTimeSettingsBuiltItem(MSBuildCommand build, string settingsPath, string markerPath)
        {
            var designTimeBuild = new MSBuildCommand(
                Log, "CollectUpToDateCheckBuiltDesignTime", build.FullPathProjectFile);
            var result = designTimeBuild.Execute(
                "-getItem:UpToDateCheckBuilt", "-nologo",
                "/p:DesignTimeBuild=true", "/p:BuildingInsideVisualStudio=true");
            result.Should().Pass();

            using var document = JsonDocument.Parse(result.StdOut);
            var marker = document.RootElement.GetProperty("Items").GetProperty("UpToDateCheckBuilt")
                .EnumerateArray().Single(item =>
                    item.GetProperty("Identity").GetString()?.EndsWith(SettingsBuildMarkerFileName, StringComparison.Ordinal) == true);
            var projectRoot = Path.GetDirectoryName(build.FullPathProjectFile);
            Assert.AreEqual(markerPath, Path.GetFullPath(Path.Combine(projectRoot, marker.GetProperty("Identity").GetString())));
            Assert.AreEqual(settingsPath, Path.GetFullPath(Path.Combine(projectRoot, marker.GetProperty("Original").GetString())));
        }

        [TestMethod]
        public void Build_GeneratesKeyPairInDeterministicIntermediatePaths()
        {
            var projectDirectory = CreateAspNetSdkTestAsset(TestAsset);
            var build = CreateBuildCommand(projectDirectory);

            ExecuteCommand(build).Should().Pass();

            var generated = GeneratedDirectory(build);
            new FileInfo(Path.Combine(generated, PublicKeyFileName)).Should().Exist();
            new FileInfo(Path.Combine(generated, PrivateKeyFileName)).Should().Exist();

            using var publicDocument = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(generated, PublicKeyFileName)));
            var publicRoot = publicDocument.RootElement;
            Assert.AreEqual(1, publicRoot.GetProperty("version").GetInt32());
            Assert.AreEqual("RSA-OAEP-SHA256", publicRoot.GetProperty("algorithm").GetString());
            Assert.AreEqual("SubjectPublicKeyInfo", publicRoot.GetProperty("format").GetString());
            var publicKey = publicRoot.GetProperty("publicKey").GetString();
            publicKey.Should().NotBeNullOrEmpty();

            using var privateDocument = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(generated, PrivateKeyFileName)));
            var privateRoot = privateDocument.RootElement;
            Assert.AreEqual("RSAParameters", privateRoot.GetProperty("format").GetString());
            Assert.AreEqual(publicKey, privateRoot.GetProperty("publicKey").GetString());

            // Every component dotnet-watch needs to import the key has to be present.
            var parameters = privateRoot.GetProperty("parameters");
            foreach (var name in new[] { "modulus", "exponent", "d", "p", "q", "dp", "dq", "inverseQ" })
            {
                parameters.GetProperty(name).GetString().Should().NotBeNullOrEmpty();
            }
        }

        /// <summary>
        /// The public half is the only part of the key pair the application may learn about. The
        /// private half must never appear in a static web asset, in an endpoint or in the output
        /// directory.
        /// </summary>
        [TestMethod]
        public void Build_PinsOnlyThePublicHalfOfTheKeyPair()
        {
            var projectDirectory = CreateAspNetSdkTestAsset(TestAsset);
            var build = CreateBuildCommand(projectDirectory);

            ExecuteCommand(build).Should().Pass();

            var generated = GeneratedDirectory(build);
            using var publicDocument = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(generated, PublicKeyFileName)));
            var publicKey = publicDocument.RootElement.GetProperty("publicKey").GetString();

            using var privateDocument = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(generated, PrivateKeyFileName)));
            var privateModulus = privateDocument.RootElement.GetProperty("parameters").GetProperty("d").GetString();

            var config = File.ReadAllText(Path.Combine(generated, ConfigFileName));
            config.Should().Contain(publicKey);
            config.Should().NotContain(privateModulus);
            config.Should().Contain("/_framework/dotnet-browser-tools/connect");
            config.Should().Contain("/_framework/dotnet-browser-tools/clear-cache");

            // The configuration only carries data; the executable client is a separate module that
            // it imports from the application's own origin.
            config.Should().Contain($"./{ClientFileName}");
            new FileInfo(Path.Combine(generated, ClientFileName)).Should().Exist();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(
                File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json")));

            // The key files themselves are not static web assets.
            manifest.Assets.Should().NotContain(a =>
                a.RelativePath.Contains("browser-tools-key") || a.RelativePath.Contains(SettingsBuildMarkerFileName));

            Directory.GetFiles(
                build.GetOutputDirectory(DefaultTfm, "Debug").ToString(),
                "browser-tools-key.*",
                SearchOption.AllDirectories).Should().BeEmpty();
        }

        /// <summary>
        /// The initializer checks the application-hosted settings before resolving the generated configuration
        /// relative to itself.
        /// </summary>
        [TestMethod]
        public void Build_InitializerResolvesConfigurationRelativeToItself()
        {
            var projectDirectory = CreateAspNetSdkTestAsset(TestAsset);
            var build = CreateBuildCommand(projectDirectory);

            ExecuteCommand(build).Should().Pass();

            var initializer = File.ReadAllText(Path.Combine(GeneratedDirectory(build), InitializerFileName));

            initializer.Should().Contain("const settingsPath = '/_framework/dotnet-browser-tools/hot-reload-settings.json';");
            initializer.Should().Contain($"const configModulePath = './{ConfigFileName}';");
            initializer.Should().Contain("settings?.hotReload === true");
            initializer.Should().Contain("cache: 'no-store'");
            initializer.Should().Contain("'If-None-Match': `\"browser-tools-${crypto.randomUUID()}\"`");
            initializer.Should().Contain("signal: controller.signal");
            initializer.Should().Contain("isHotReloadEnabled");
            initializer.Should().NotContain("__SETTINGS_PATH__");
            initializer.Should().NotContain("__CONFIG_MODULE__");
        }

        [TestMethod]
        public void Build_RegistersMutableSettingsWithoutFingerprintOrCompression()
        {
            var projectDirectory = CreateAspNetSdkTestAsset(TestAsset);
            var build = CreateBuildCommand(projectDirectory);
            ExecuteCommand(build, "/p:StaticWebAssetsFingerprintContent=true").Should().Pass();

            var settingsFile = Path.Combine(GeneratedDirectory(build), SettingsFileName);
            Assert.AreEqual("{ \"hotReload\": false }" + Environment.NewLine, File.ReadAllText(settingsFile));

            var intermediate = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(
                File.ReadAllBytes(Path.Combine(intermediate, "staticwebassets.build.json")));
            var settings = manifest.Assets.Where(a => a.RelativePath.Replace('\\', '/') == SettingsRoute.TrimStart('/')).ToArray();
            settings.Should().ContainSingle();
            settings[0].IsBuildOnly().Should().BeTrue();
            settings[0].IsPrimaryAsset().Should().BeTrue();
            manifest.Assets.Should().NotContain(a =>
                a.IsAlternativeAsset() && a.RelatedAsset == settings[0].Identity);

            var endpoints = JsonSerializer.Deserialize<StaticWebAssetEndpointsManifest>(
                File.ReadAllText(Path.Combine(intermediate, "staticwebassets.build.endpoints.json")));
            var settingsEndpoints = endpoints.Endpoints.Where(e => e.Route == SettingsRoute.TrimStart('/')).ToArray();
            settingsEndpoints.Should().ContainSingle();
            settingsEndpoints[0].Order.Should().Be("-1001");
            settingsEndpoints[0].Selectors.Should().BeEmpty();
            settingsEndpoints[0].ResponseHeaders.Should().Contain(h => h.Name == "Cache-Control" && h.Value == "no-store");
            endpoints.Endpoints.Should().NotContain(e =>
                e.Route.Contains("hot-reload-settings.") && e.Route != SettingsRoute.TrimStart('/'));
        }

        [TestMethod]
        [DataRow("RazorComponentApp", "ComponentApp.csproj", false)]
        [DataRow("BlazorWasmTestApp", "BlazorWasmTestApp.csproj", true)]
        public async Task DevelopmentHost_SeesSettingsChangesWithoutRebuildOrRestart(
            string assetName, string projectName, bool gateway)
        {
            var projectDirectory = CreateAspNetSdkTestAsset(assetName, identifier: "mutable-browser-settings");
            if (!gateway)
            {
                File.WriteAllText(Path.Combine(projectDirectory.TestRoot, "Program.cs"), """
                    using Microsoft.AspNetCore.Builder;
                    var builder = WebApplication.CreateBuilder(args);
                    var app = builder.Build();
                    app.MapStaticAssets();
                    app.MapGet("/host-id", () => System.Environment.ProcessId.ToString());
                    app.Run();
                    """);
            }

            var build = CreateBuildCommand(projectDirectory, projectName);
            ExecuteCommand(build).Should().Pass();
            var settingsFile = Path.Combine(GeneratedDirectory(build), SettingsFileName);
            Assert.AreEqual("{ \"hotReload\": false }" + Environment.NewLine, File.ReadAllText(settingsFile));
            var initializer = Path.Combine(
                GeneratedDirectory(build),
                gateway ? "Microsoft.NET.Sdk.WebAssembly.DotNetWatch.lib.module.js" : InitializerFileName);
            File.ReadAllText(initializer).Should().Contain("'If-None-Match': `\"browser-tools-${crypto.randomUUID()}\"`");

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            var start = new ProcessStartInfo(SdkTestContext.Current.ToolsetUnderTest.DotNetHostPath)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(build.FullPathProjectFile),
            };
            start.ArgumentList.Add("run");
            start.ArgumentList.Add("--no-build");
            start.ArgumentList.Add("--no-launch-profile");
            start.ArgumentList.Add("--project");
            start.ArgumentList.Add(build.FullPathProjectFile);
            start.ArgumentList.Add("--urls");
            start.ArgumentList.Add($"http://127.0.0.1:{port}");
            start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            start.Environment["DOTNET_ENVIRONMENT"] = "Development";
            start.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
            if (gateway)
            {
                const string cluster = "dotnet-browser-tools";
                foreach (var route in new[] { "connect", "clear-cache" })
                {
                    var name = $"{cluster}-{route}";
                    start.Environment[$"ReverseProxy__Routes__{name}__ClusterId"] = cluster;
                    start.Environment[$"ReverseProxy__Routes__{name}__Order"] = "-1000";
                    start.Environment[$"ReverseProxy__Routes__{name}__Match__Path"] = $"/_framework/dotnet-browser-tools/{route}";
                }

                start.Environment[$"ReverseProxy__Clusters__{cluster}__Destinations__provider__Address"] = "http://127.0.0.1:1/";
            }

            using var host = Process.Start(start);
            Assert.IsNotNull(host);
            try
            {
                using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                async Task AssertServedAsync(bool enabled)
                {
                    while (true)
                    {
                        if (host.HasExited)
                        {
                            Assert.Fail($"The development host exited with {host.ExitCode}");
                        }
                        try
                        {
                            using var request = new HttpRequestMessage(HttpMethod.Get, SettingsRoute);
                            request.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true };
                            request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue($"\"browser-tools-{Guid.NewGuid():N}\""));
                            using var response = await http.SendAsync(request, timeout.Token);
                            if (response.StatusCode != HttpStatusCode.OK)
                            {
                                Assert.Fail($"Fresh validator received {(int)response.StatusCode} from the development host");
                            }

                            Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);
                            var body = await response.Content.ReadAsStringAsync(timeout.Token);
                            Console.WriteLine($"Fresh: status={response.StatusCode}, body={body.Trim()}, cache={response.Headers.CacheControl}, etag={response.Headers.ETag}, modified={response.Content.Headers.LastModified}, host={host.Id}");
                            Assert.AreEqual(
                                enabled ? "{ \"hotReload\": true }" : "{ \"hotReload\": false }",
                                body.Trim());
                            return;
                        }
                        catch (HttpRequestException) when (!timeout.IsCancellationRequested)
                        {
                            await Task.Delay(200, timeout.Token);
                        }
                    }
                }

                await AssertServedAsync(false);
                using var initialResponse = await http.GetAsync(SettingsRoute, timeout.Token);
                var initialEtag = initialResponse.Headers.ETag;
                Assert.IsNotNull(initialEtag);
                Console.WriteLine($"Initial: status={initialResponse.StatusCode}, body={await initialResponse.Content.ReadAsStringAsync(timeout.Token)}, cache={initialResponse.Headers.CacheControl}, etag={initialEtag}, modified={initialResponse.Content.Headers.LastModified}");
                var hostId = host.Id;
                var applicationId = gateway ? null : await http.GetStringAsync("/host-id", timeout.Token);
                var originalWriteTime = File.GetLastWriteTimeUtc(settingsFile);

                await Task.Delay(100, timeout.Token);
                File.WriteAllText(settingsFile, "{ \"hotReload\": true }" + Environment.NewLine);
                await AssertServedAsync(true);
                using var conditionalRequest = new HttpRequestMessage(HttpMethod.Get, SettingsRoute);
                conditionalRequest.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(initialEtag.ToString()));
                using var conditionalResponse = await http.SendAsync(conditionalRequest, timeout.Token);
                var conditionalBody = await conditionalResponse.Content.ReadAsStringAsync(timeout.Token);
                Console.WriteLine($"Conditional: status={conditionalResponse.StatusCode}, body={conditionalBody}, cache={conditionalResponse.Headers.CacheControl}, etag={conditionalResponse.Headers.ETag}, modified={conditionalResponse.Content.Headers.LastModified}");
                Assert.AreEqual(HttpStatusCode.NotModified, conditionalResponse.StatusCode);
                Assert.AreEqual("", conditionalBody);
                var watchWriteTime = File.GetLastWriteTimeUtc(settingsFile);
                Assert.AreNotEqual(originalWriteTime, watchWriteTime);

                // BrowserToolsBuildOutputsTests covers watch's write-if-different operation.
                Assert.AreEqual("{ \"hotReload\": true }" + Environment.NewLine, File.ReadAllText(settingsFile));
                Assert.AreEqual(watchWriteTime, File.GetLastWriteTimeUtc(settingsFile));
                await AssertServedAsync(true);

                ExecuteCommand(CreateBuildCommand(projectDirectory, projectName)).Should().Pass();
                await AssertServedAsync(false);
                var runWriteTime = File.GetLastWriteTimeUtc(settingsFile);
                ExecuteCommand(CreateBuildCommand(projectDirectory, projectName)).Should().Pass();
                Assert.AreEqual(runWriteTime, File.GetLastWriteTimeUtc(settingsFile));
                Assert.AreEqual("{ \"hotReload\": false }" + Environment.NewLine, File.ReadAllText(settingsFile));
                await AssertServedAsync(false);
                Assert.AreEqual(hostId, host.Id);
                if (!gateway)
                {
                    Assert.AreEqual(applicationId, await http.GetStringAsync("/host-id", timeout.Token));
                }
                Assert.IsFalse(host.HasExited);
            }
            finally
            {
                if (!host.HasExited)
                {
                    host.Kill(entireProcessTree: true);
                    host.WaitForExit();
                }
            }
        }

        [TestMethod]
        public void Build_BrowserToolsUiIsCompatibleWithStrictCsp()
        {
            var projectDirectory = CreateAspNetSdkTestAsset(TestAsset);
            var build = CreateBuildCommand(projectDirectory);

            ExecuteCommand(build).Should().Pass();

            var client = File.ReadAllText(Path.Combine(GeneratedDirectory(build), ClientFileName));
            client.Should().Contain("attachShadow({ mode: 'open' })");
            client.Should().Contain("root.adoptedStyleSheets = [browserToolsStylesheet]");
            client.Should().Contain("browserToolsStylesheet.replaceSync(browserToolsStyles)");
            client.Should().NotContain("innerHTML");
            client.Should().NotContain("setAttribute('style'");
            client.Should().NotContain("createElement('style'");
            client.Should().NotContain(".style.");
        }

        [TestMethod]
        public void Build_RegistersAssetsAsBuildOnlyAndKeepsThemOutOfTheOutputDirectory()
        {
            var projectDirectory = CreateAspNetSdkTestAsset(TestAsset);
            var build = CreateBuildCommand(projectDirectory);

            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(
                File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json")));

            var assets = manifest.Assets.Where(a => a.RelativePath.Contains("DotNetWatch.BrowserTools")).ToArray();

            // The client and its configuration, plus the compressed alternative of each.
            assets.Where(a => a.IsPrimaryAsset()).Should().HaveCount(2);
            assets.Should().OnlyContain(a => a.IsBuildOnly());

            Directory.GetFiles(
                build.GetOutputDirectory(DefaultTfm, "Debug").ToString(),
                "*BrowserTools*",
                SearchOption.AllDirectories).Should().BeEmpty();
        }

        /// <summary>
        /// The modules are imported by name from the application, so both the fingerprinted and the
        /// plain route have to resolve.
        /// </summary>
        [TestMethod]
        public void Build_DefinesEndpointsForTheGeneratedModules()
        {
            var projectDirectory = CreateAspNetSdkTestAsset(TestAsset);
            var build = CreateBuildCommand(projectDirectory);

            ExecuteCommand(build).Should().Pass();

            var endpoints = File.ReadAllText(Path.Combine(
                build.GetOutputDirectory(DefaultTfm, "Debug").ToString(),
                "ComponentApp.staticwebassets.endpoints.json"));

            endpoints.Should().Contain($"_framework/{ConfigFileName}");
            endpoints.Should().Contain($"_framework/{ClientFileName}");
        }

        /// <summary>
        /// Browser tools are a development only feature. Nothing may be generated when they are off,
        /// including the key pair.
        /// </summary>
        [TestMethod]
        public void Build_WithHotReloadDisabled_DoesNotGenerateAnything()
        {
            var projectDirectory = CreateAspNetSdkTestAsset(TestAsset);
            var build = CreateBuildCommand(projectDirectory);

            ExecuteCommand(build, "/p:EnableHotReloadInRuntimeConfigDevFile=false").Should().Pass();

            new DirectoryInfo(GeneratedDirectory(build)).Should().NotExist();
        }

        [TestMethod]
        public void Build_InReleaseConfiguration_DoesNotGenerateOutputs()
        {
            var projectDirectory = CreateAspNetSdkTestAsset(TestAsset);
            var build = CreateBuildCommand(projectDirectory);

            ExecuteCommand(build, "/p:Configuration=Release").Should().Pass();

            new DirectoryInfo(Path.Combine(
                build.GetIntermediateDirectory(DefaultTfm, "Release").ToString(),
                "dotnet-watch")).Should().NotExist();
        }

        [TestMethod]
        public void Build_InReleaseConfigurationWithHotReloadEnabled_GeneratesOutputs()
        {
            var projectDirectory = CreateAspNetSdkTestAsset(TestAsset);
            var build = CreateBuildCommand(projectDirectory);

            ExecuteCommand(
                build,
                "/p:Configuration=Release",
                "/p:EnableHotReloadInRuntimeConfigDevFile=true").Should().Pass();

            var generated = Path.Combine(
                build.GetIntermediateDirectory(DefaultTfm, "Release").ToString(),
                "dotnet-watch");
            new FileInfo(Path.Combine(generated, PublicKeyFileName)).Should().Exist();
            new FileInfo(Path.Combine(generated, InitializerFileName)).Should().Exist();
        }

        [TestMethod]
        public void Build_HostedWebAssembly_ClientOwnsBrowserToolsOutputs()
        {
            var testInstance = CreateAspNetSdkTestAsset("BlazorHosted");
            var build = CreateBuildCommand(testInstance, "blazorhosted");

            ExecuteCommand(build).Should().Pass();

            var hostGenerated = Path.Combine(
                build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString(),
                "dotnet-watch");
            var clientIntermediate = OutputPathCalculator
                .FromProject(Path.Combine(testInstance.TestRoot, "blazorwasm"))
                .GetIntermediateDirectory(DefaultTfm, "Debug")
                .ToString();
            var clientGenerated = Path.Combine(clientIntermediate, "dotnet-watch");

            new DirectoryInfo(hostGenerated).Should().NotExist();
            new FileInfo(Path.Combine(clientGenerated, PublicKeyFileName)).Should().Exist();
            new FileInfo(Path.Combine(clientGenerated, PrivateKeyFileName)).Should().Exist();
            new FileInfo(Path.Combine(clientGenerated, SettingsBuildMarkerFileName)).Should().Exist();

            var hostManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(
                build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString(),
                "staticwebassets.build.json")));
            var initializerAssets = hostManifest.Assets
                .Where(a => a.RelativePath.Replace('\\', '/').Contains("Microsoft.NET.Sdk.WebAssembly.DotNetWatch"))
                .Where(a => a.IsPrimaryAsset())
                .ToArray();

            initializerAssets.Should().NotBeEmpty();
            initializerAssets.Should().OnlyContain(a => a.SourceId == "blazorwasm");
        }

        /// <summary>
        /// The key pair is reused as long as it is valid, so an incremental rebuild must not rewrite
        /// the generated files: rewriting them would rotate the key the running browser pinned and
        /// invalidate downstream incrementality.
        /// </summary>
        [TestMethod]
        public void Rebuild_ReusesTheExistingKeyPair()
        {
            var projectDirectory = CreateAspNetSdkTestAsset(TestAsset);
            var build = CreateBuildCommand(projectDirectory);

            ExecuteCommand(build).Should().Pass();

            var generated = GeneratedDirectory(build);
            var configPath = Path.Combine(generated, ConfigFileName);
            var publicKeyPath = Path.Combine(generated, PublicKeyFileName);
            var privateKeyPath = Path.Combine(generated, PrivateKeyFileName);

            var configThumbprint = FileThumbPrint.Create(configPath);
            var publicKeyThumbprint = FileThumbPrint.Create(publicKeyPath);
            var privateKeyThumbprint = FileThumbPrint.Create(privateKeyPath);

            ExecuteCommand(CreateBuildCommand(projectDirectory)).Should().Pass();

            Assert.AreEqual(publicKeyThumbprint, FileThumbPrint.Create(publicKeyPath));
            Assert.AreEqual(privateKeyThumbprint, FileThumbPrint.Create(privateKeyPath));
            Assert.AreEqual(configThumbprint, FileThumbPrint.Create(configPath));
        }

        [TestMethod]
        public void Rebuild_ResetsChangedSettingsWithoutRewritingUnchangedAssets()
        {
            var projectDirectory = CreateAspNetSdkTestAsset(TestAsset);
            var build = CreateBuildCommand(projectDirectory);
            ExecuteCommand(build).Should().Pass();

            var generated = GeneratedDirectory(build);
            var settingsPath = Path.Combine(generated, SettingsFileName);
            var markerPath = Path.Combine(generated, SettingsBuildMarkerFileName);
            var configPath = Path.Combine(generated, ConfigFileName);
            var manifestPath = Path.Combine(build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString(), "staticwebassets.build.json");
            var originalSettings = File.ReadAllBytes(settingsPath);
            var initialSettingsTime = File.GetLastWriteTimeUtc(settingsPath);
            var originalConfigTime = File.GetLastWriteTimeUtc(configPath);

            Assert.IsGreaterThanOrEqualTo(initialSettingsTime, File.GetLastWriteTimeUtc(markerPath));
            File.WriteAllText(settingsPath, "{ \"hotReload\": true }" + Environment.NewLine);
            Assert.IsGreaterThan(File.GetLastWriteTimeUtc(markerPath), File.GetLastWriteTimeUtc(settingsPath));

            ExecuteCommand(CreateBuildCommand(projectDirectory)).Should().Pass();

            Assert.AreSequenceEqual(originalSettings, File.ReadAllBytes(settingsPath));
            Assert.IsGreaterThan(initialSettingsTime, File.GetLastWriteTimeUtc(settingsPath));
            Assert.IsGreaterThanOrEqualTo(File.GetLastWriteTimeUtc(settingsPath), File.GetLastWriteTimeUtc(markerPath));
            Assert.AreEqual(originalConfigTime, File.GetLastWriteTimeUtc(configPath));
            var resetManifest = File.ReadAllBytes(manifestPath);
            var manifestAsset = StaticWebAssetsManifest.FromJsonBytes(resetManifest).Assets
                .Single(asset => asset.Identity == settingsPath);
            Assert.AreEqual(
                File.GetLastWriteTimeUtc(settingsPath).Ticks / TimeSpan.TicksPerSecond,
                manifestAsset.LastWriteTime.UtcDateTime.Ticks / TimeSpan.TicksPerSecond);

            var resetSettingsTime = File.GetLastWriteTimeUtc(settingsPath);
            ExecuteCommand(CreateBuildCommand(projectDirectory)).Should().Pass();
            Assert.AreSequenceEqual(originalSettings, File.ReadAllBytes(settingsPath));
            Assert.AreEqual(resetSettingsTime, File.GetLastWriteTimeUtc(settingsPath));
            Assert.IsGreaterThanOrEqualTo(resetSettingsTime, File.GetLastWriteTimeUtc(markerPath));
            Assert.AreEqual(originalConfigTime, File.GetLastWriteTimeUtc(configPath));
            Assert.AreSequenceEqual(resetManifest, File.ReadAllBytes(manifestPath));
        }

        [TestMethod]
        [DataRow("RazorComponentApp", "ComponentApp.csproj")]
        [DataRow("BlazorWasmTestApp", "BlazorWasmTestApp.csproj")]
        public void VisualStudioFastUpToDateCheck_BuildsOnlyAfterSettingsChange(
            string testAsset, string projectName)
        {
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset)
                .WithProjectChanges(project => project.Root.Add(new XElement("Target",
                    new XAttribute("Name", "CollectUpToDateCheckBuiltDesignTime"))));
            var build = CreateBuildCommand(projectDirectory, projectName);
            ExecuteCommand(build).Should().Pass();

            var settingsPath = Path.Combine(GeneratedDirectory(build), SettingsFileName);
            var markerPath = Path.Combine(GeneratedDirectory(build), SettingsBuildMarkerFileName);

            AssertDesignTimeSettingsBuiltItem(build, settingsPath, markerPath);

            // Simulate the project-system check of the design-time UpToDateCheckBuilt/Original pair.
            // Visual Studio skips MSBuild when the source is no newer than its built output.
            var buildsScheduled = 0;
            void BuildIfOutOfDate()
            {
                if (!SettingsRequireBuild(settingsPath, markerPath))
                {
                    return;
                }

                ExecuteCommand(
                    CreateBuildCommand(projectDirectory, projectName),
                    "/p:BuildingInsideVisualStudio=true").Should().Pass();
                buildsScheduled++;
            }

            BuildIfOutOfDate();
            Assert.AreEqual(0, buildsScheduled);
            var unchangedSettingsTime = File.GetLastWriteTimeUtc(settingsPath);
            Assert.AreEqual("{ \"hotReload\": false }" + Environment.NewLine, File.ReadAllText(settingsPath));

            File.WriteAllText(settingsPath, "{ \"hotReload\": true }" + Environment.NewLine);
            Assert.IsGreaterThan(File.GetLastWriteTimeUtc(markerPath), File.GetLastWriteTimeUtc(settingsPath));

            BuildIfOutOfDate();
            Assert.AreEqual(1, buildsScheduled);
            Assert.AreEqual("{ \"hotReload\": false }" + Environment.NewLine, File.ReadAllText(settingsPath));
            Assert.IsGreaterThan(unchangedSettingsTime, File.GetLastWriteTimeUtc(settingsPath));
            Assert.IsGreaterThanOrEqualTo(File.GetLastWriteTimeUtc(settingsPath), File.GetLastWriteTimeUtc(markerPath));

            var resetSettingsTime = File.GetLastWriteTimeUtc(settingsPath);
            BuildIfOutOfDate();
            Assert.AreEqual(1, buildsScheduled);
            Assert.AreEqual(resetSettingsTime, File.GetLastWriteTimeUtc(settingsPath));
            Assert.AreEqual("{ \"hotReload\": false }" + Environment.NewLine, File.ReadAllText(settingsPath));
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void VisualStudioFastUpToDateCheck_RebuildsWhenSettingsOrMarkerIsMissing(bool removeMarker)
        {
            var projectDirectory = CreateAspNetSdkTestAsset(TestAsset, identifier: removeMarker.ToString())
                .WithProjectChanges(project => project.Root.Add(new XElement("Target",
                    new XAttribute("Name", "CollectUpToDateCheckBuiltDesignTime"))));
            var build = CreateBuildCommand(projectDirectory);
            ExecuteCommand(build).Should().Pass();

            var settingsPath = Path.Combine(GeneratedDirectory(build), SettingsFileName);
            var markerPath = Path.Combine(GeneratedDirectory(build), SettingsBuildMarkerFileName);
            File.Delete(removeMarker ? markerPath : settingsPath);

            AssertDesignTimeSettingsBuiltItem(build, settingsPath, markerPath);

            var built = false;
            if (SettingsRequireBuild(settingsPath, markerPath))
            {
                ExecuteCommand(CreateBuildCommand(projectDirectory), "/p:BuildingInsideVisualStudio=true").Should().Pass();
                built = true;
            }

            Assert.IsTrue(built);
            Assert.AreEqual("{ \"hotReload\": false }" + Environment.NewLine, File.ReadAllText(settingsPath));
            Assert.IsGreaterThanOrEqualTo(File.GetLastWriteTimeUtc(settingsPath), File.GetLastWriteTimeUtc(markerPath));
            Assert.IsFalse(SettingsRequireBuild(settingsPath, markerPath));
            AssertDesignTimeSettingsBuiltItem(build, settingsPath, markerPath);
        }

        [TestMethod]
        public void DesignTimeBuild_HostedWebAssembly_TracksTheClientSettingsOnly()
        {
            var projectDirectory = CreateAspNetSdkTestAsset("BlazorHosted")
                .WithProjectChanges(project => project.Root.Add(new XElement("Target",
                    new XAttribute("Name", "CollectUpToDateCheckBuiltDesignTime"))));
            ExecuteCommand(CreateBuildCommand(projectDirectory, "blazorhosted")).Should().Pass();

            var clientProjectPath = Path.Combine(projectDirectory.TestRoot, "blazorwasm", "blazorwasm.csproj");
            var serverProjectPath = Path.Combine(projectDirectory.TestRoot, "blazorhosted", "blazorhosted.csproj");

            static string[] GetBuiltItems(MSBuildCommand command)
            {
                var result = command.Execute("-getItem:UpToDateCheckBuilt", "-nologo", "/p:DesignTimeBuild=true");
                result.Should().Pass();
                using var document = JsonDocument.Parse(result.StdOut);
                return document.RootElement.GetProperty("Items").GetProperty("UpToDateCheckBuilt")
                    .EnumerateArray().Select(item => item.GetProperty("Identity").GetString()).ToArray();
            }

            var clientItems = GetBuiltItems(new MSBuildCommand(Log, "CollectUpToDateCheckBuiltDesignTime", clientProjectPath));
            clientItems.Should().ContainSingle(path =>
                path != null && path.EndsWith($"dotnet-watch\\{SettingsBuildMarkerFileName}", StringComparison.Ordinal));

            var serverItems = GetBuiltItems(new MSBuildCommand(Log, "CollectUpToDateCheckBuiltDesignTime", serverProjectPath));
            serverItems.Should().NotContain(path =>
                path != null && path.EndsWith($"dotnet-watch\\{SettingsBuildMarkerFileName}", StringComparison.Ordinal));
        }

        /// <summary>
        /// A key pair that no longer describes a usable key has to be replaced rather than reused,
        /// otherwise every subsequent watch session would fail to start the browser tools.
        /// </summary>
        [TestMethod]
        public void Rebuild_RegeneratesTheKeyPairWhenItIsInvalid()
        {
            var projectDirectory = CreateAspNetSdkTestAsset(TestAsset);
            var build = CreateBuildCommand(projectDirectory);

            ExecuteCommand(build).Should().Pass();

            var generated = GeneratedDirectory(build);
            var publicKeyPath = Path.Combine(generated, PublicKeyFileName);
            var privateKeyPath = Path.Combine(generated, PrivateKeyFileName);
            var configPath = Path.Combine(generated, ConfigFileName);

            var originalPublicKey = File.ReadAllText(publicKeyPath);
            var originalConfig = File.ReadAllText(configPath);

            File.WriteAllText(privateKeyPath, "not a key");

            ExecuteCommand(CreateBuildCommand(projectDirectory)).Should().Pass();

            File.ReadAllText(publicKeyPath).Should().NotBe(originalPublicKey);
            File.ReadAllText(configPath).Should().NotBe(originalConfig);

            // The regenerated halves have to describe the same key again.
            using var publicDocument = JsonDocument.Parse(File.ReadAllBytes(publicKeyPath));
            using var privateDocument = JsonDocument.Parse(File.ReadAllBytes(privateKeyPath));
            Assert.AreEqual(
                publicDocument.RootElement.GetProperty("publicKey").GetString(),
                privateDocument.RootElement.GetProperty("publicKey").GetString());
        }

        [TestMethod]
        public void Publish_NeverContainsBrowserToolsAssets()
        {
            var projectDirectory = CreateAspNetSdkTestAsset(TestAsset);
            var publish = CreatePublishCommand(projectDirectory);

            ExecuteCommand(publish).Should().Pass();

            var publishManifest = File.ReadAllText(Path.Combine(
                publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString(),
                "staticwebassets.publish.json"));

            publishManifest.Should().NotContain("DotNetWatch.BrowserTools");
            publishManifest.Should().NotContain("hot-reload-settings");
            new FileInfo(Path.Combine(publish.GetOutputDirectory(DefaultTfm, "Debug").ToString(), SettingsFileName)).Should().NotExist();
            Directory.GetFiles(
                publish.GetOutputDirectory(DefaultTfm, "Debug").ToString(),
                SettingsBuildMarkerFileName,
                SearchOption.AllDirectories).Should().BeEmpty();

            Directory.GetFiles(
                publish.GetOutputDirectory(DefaultTfm, "Debug").ToString(),
                "*BrowserTools*",
                SearchOption.AllDirectories).Should().BeEmpty();
        }

        /// <summary>
        /// The generated files, including both halves of the key pair, are tracked so that a clean
        /// removes them and no key material is left behind on disk.
        /// </summary>
        [TestMethod]
        public void Clean_RemovesTheGeneratedAssetsAndTheKeyPair()
        {
            var projectDirectory = CreateAspNetSdkTestAsset(TestAsset);
            var build = CreateBuildCommand(projectDirectory);

            ExecuteCommand(build).Should().Pass();

            var generated = GeneratedDirectory(build);
            new FileInfo(Path.Combine(generated, ConfigFileName)).Should().Exist();
            new FileInfo(Path.Combine(generated, PrivateKeyFileName)).Should().Exist();

            var clean = new MSBuildCommand(Log, "Clean", build.FullPathProjectFile);
            ExecuteCommand(clean).Should().Pass();

            new FileInfo(Path.Combine(generated, ConfigFileName)).Should().NotExist();
            new FileInfo(Path.Combine(generated, PublicKeyFileName)).Should().NotExist();
            new FileInfo(Path.Combine(generated, PrivateKeyFileName)).Should().NotExist();
            new FileInfo(Path.Combine(generated, SettingsFileName)).Should().NotExist();
            new FileInfo(Path.Combine(generated, SettingsBuildMarkerFileName)).Should().NotExist();
        }

        /// <summary>
        /// After a clean the key pair has to be recreated, and the pinned configuration has to
        /// follow it.
        /// </summary>
        [TestMethod]
        public void BuildAfterClean_RegeneratesTheKeyPair()
        {
            var projectDirectory = CreateAspNetSdkTestAsset(TestAsset);
            var build = CreateBuildCommand(projectDirectory);

            ExecuteCommand(build).Should().Pass();

            var generated = GeneratedDirectory(build);
            var originalPublicKey = File.ReadAllText(Path.Combine(generated, PublicKeyFileName));

            var clean = new MSBuildCommand(Log, "Clean", build.FullPathProjectFile);
            ExecuteCommand(clean).Should().Pass();

            ExecuteCommand(CreateBuildCommand(projectDirectory)).Should().Pass();

            new FileInfo(Path.Combine(generated, PublicKeyFileName)).Should().Exist();
            File.ReadAllText(Path.Combine(generated, PublicKeyFileName)).Should().NotBe(originalPublicKey);
        }
    }
}
