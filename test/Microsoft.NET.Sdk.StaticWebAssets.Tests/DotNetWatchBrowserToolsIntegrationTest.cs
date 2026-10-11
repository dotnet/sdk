// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

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

        // Contract with Microsoft.DotNet.Watch.BrowserToolsBuildOutputs.
        private const string PublicKeyFileName = "browser-tools-key.public.json";
        private const string PrivateKeyFileName = "browser-tools-key.private.json";

        private string GeneratedDirectory(MSBuildCommand command)
            => Path.Combine(command.GetIntermediateDirectory(DefaultTfm, "Debug").ToString(), "dotnet-watch");

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
            manifest.Assets.Should().NotContain(a => a.RelativePath.Contains("browser-tools-key"));

            Directory.GetFiles(
                build.GetOutputDirectory(DefaultTfm, "Debug").ToString(),
                "browser-tools-key.*",
                SearchOption.AllDirectories).Should().BeEmpty();
        }

        /// <summary>
        /// The initializer checks provider availability before resolving the generated configuration
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
            initializer.Should().Contain("signal: controller.signal");
            initializer.Should().Contain("isHotReloadEnabled");
            initializer.Should().NotContain("__SETTINGS_PATH__");
            initializer.Should().NotContain("__CONFIG_MODULE__");
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
