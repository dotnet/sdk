// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.NET.Sdk.BlazorWebAssembly.Tests;
using static Microsoft.NET.Sdk.BlazorWebAssembly.Tests.ServiceWorkerAssert;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.AoT.Tests
{
    public class WasmAoTPublishIntegrationTest : WasmPublishIntegrationTestBase
    {
        public WasmAoTPublishIntegrationTest(ITestOutputHelper log) : base(log) { }

        [Fact]
        public void AoT_Publish_InRelease_Works()
        {
            // Diagnostic: dump SDK layout and environment to diagnose MSB4216 task host failures
            var sdkFolder = SdkTestContext.Current.ToolsetUnderTest.SdkFolderUnderTest;
            var dotNetRoot = SdkTestContext.Current.ToolsetUnderTest.DotNetRoot;
            Log.WriteLine($"[DIAG] SDK folder under test: {sdkFolder}");
            Log.WriteLine($"[DIAG] DotNetRoot: {dotNetRoot}");
            Log.WriteLine($"[DIAG] DotNetHostPath: {SdkTestContext.Current.ToolsetUnderTest.DotNetHostPath}");
            Log.WriteLine($"[DIAG] DOTNET_HOST_PATH env: {Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")}");
            Log.WriteLine($"[DIAG] DOTNET_ROOT env: {Environment.GetEnvironmentVariable("DOTNET_ROOT")}");
            Log.WriteLine($"[DIAG] OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
            Log.WriteLine($"[DIAG] Process arch: {RuntimeInformation.ProcessArchitecture}");

            if (sdkFolder != null && Directory.Exists(sdkFolder))
            {
                var msbuildFiles = Directory.GetFiles(sdkFolder, "MSBuild*");
                Log.WriteLine($"[DIAG] MSBuild* files in SDK ({sdkFolder}):");
                foreach (var f in msbuildFiles)
                {
                    var fi = new FileInfo(f);
                    Log.WriteLine($"[DIAG]   {fi.Name} ({fi.Length} bytes)");
                }

                // Check if the MSBuild apphost exists (no extension on Unix, .exe on Windows)
                string apphostName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "MSBuild.exe" : "MSBuild";
                string apphostPath = Path.Combine(sdkFolder, apphostName);
                Log.WriteLine($"[DIAG] Apphost path: {apphostPath}");
                Log.WriteLine($"[DIAG] Apphost exists: {File.Exists(apphostPath)}");

                if (File.Exists(apphostPath) && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Check Unix file permissions via ls
                    try
                    {
                        var lsResult = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ls", $"-la \"{apphostPath}\"")
                        {
                            RedirectStandardOutput = true,
                            UseShellExecute = false
                        });
                        if (lsResult != null)
                        {
                            string lsOutput = lsResult.StandardOutput.ReadToEnd();
                            lsResult.WaitForExit();
                            Log.WriteLine($"[DIAG] ls -la apphost: {lsOutput.Trim()}");
                        }
                    }
                    catch (Exception lsEx)
                    {
                        Log.WriteLine($"[DIAG] ls failed: {lsEx.Message}");
                    }

                    // Try to actually execute the apphost with --help to verify it can run
                    try
                    {
                        var testRun = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(apphostPath, "--help")
                        {
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            Environment = { ["DOTNET_ROOT"] = dotNetRoot }
                        });
                        if (testRun != null)
                        {
                            string stdout = testRun.StandardOutput.ReadToEnd();
                            string stderr = testRun.StandardError.ReadToEnd();
                            testRun.WaitForExit(5000);
                            Log.WriteLine($"[DIAG] Apphost test run exit code: {testRun.ExitCode}");
                            if (!string.IsNullOrEmpty(stdout)) Log.WriteLine($"[DIAG] Apphost stdout: {stdout.Substring(0, Math.Min(500, stdout.Length))}");
                            if (!string.IsNullOrEmpty(stderr)) Log.WriteLine($"[DIAG] Apphost stderr: {stderr.Substring(0, Math.Min(500, stderr.Length))}");
                        }
                    }
                    catch (Exception runEx)
                    {
                        Log.WriteLine($"[DIAG] Apphost test run failed: {runEx.Message}");
                    }
                }

                // Check runtimeconfig
                string rcPath = Path.Combine(sdkFolder, "MSBuild.runtimeconfig.json");
                Log.WriteLine($"[DIAG] MSBuild.runtimeconfig.json exists: {File.Exists(rcPath)}");
                if (File.Exists(rcPath))
                {
                    Log.WriteLine($"[DIAG] runtimeconfig content: {File.ReadAllText(rcPath)}");
                }

                // Check dotnet host exists and is executable
                string dotnetPath = Path.Combine(dotNetRoot, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet");
                Log.WriteLine($"[DIAG] dotnet path: {dotnetPath}");
                Log.WriteLine($"[DIAG] dotnet exists: {File.Exists(dotnetPath)}");
            }

            // Diagnostic: dump all DOTNET_* env vars from the test runner process
            Log.WriteLine($"[DIAG] === Environment variables (DOTNET_*) ===");
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                string key = entry.Key?.ToString() ?? "";
                if (key.StartsWith("DOTNET", StringComparison.OrdinalIgnoreCase) || key.StartsWith("MSBUILD", StringComparison.OrdinalIgnoreCase))
                {
                    Log.WriteLine($"[DIAG] ENV {key}={entry.Value}");
                }
            }

            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAssetWithAot(testAppName, new[] { "blazorwasm" })
                .WithProjectChanges((p, doc) =>
                {
                    var itemGroup = new XElement("PropertyGroup");
                    itemGroup.Add(new XElement("WasmFingerprintAssets", false));
                    doc.Root.Add(itemGroup);

                    // Inject a diagnostic target that dumps task host-relevant MSBuild properties
                    if (Path.GetFileName(p) == "blazorwasm.csproj")
                    {
                        var diagTarget = new XElement("Target",
                            new XAttribute("Name", "DiagDumpTaskHostProps"),
                            new XAttribute("BeforeTargets", "Build;Publish"),
                            new XElement("Message",
                                new XAttribute("Importance", "High"),
                                new XAttribute("Text", "[DIAG-PROP] DOTNET_HOST_PATH=$(DOTNET_HOST_PATH)")),
                            new XElement("Message",
                                new XAttribute("Importance", "High"),
                                new XAttribute("Text", "[DIAG-PROP] NetCoreSdkRoot=$(NetCoreSdkRoot)")),
                            new XElement("Message",
                                new XAttribute("Importance", "High"),
                                new XAttribute("Text", "[DIAG-PROP] MSBuildToolsPath=$(MSBuildToolsPath)")),
                            new XElement("Message",
                                new XAttribute("Importance", "High"),
                                new XAttribute("Text", "[DIAG-PROP] DOTNET_ROOT=$(DOTNET_ROOT)")),
                            new XElement("Message",
                                new XAttribute("Importance", "High"),
                                new XAttribute("Text", "[DIAG-PROP] MSBUILD_EXE_PATH=$(MSBUILD_EXE_PATH)")));
                        doc.Root.Add(diagTarget);
                    }
                });

            File.WriteAllText(Path.Combine(testInstance.TestRoot, "blazorwasm", "App.razor.css"), "h1 { font-size: 16px; }");

            var publishCommand = new PublishCommand(testInstance, "blazorwasm");
            var result = publishCommand.Execute("/p:Configuration=Release");

            // Log the full output before asserting, so diagnostics are captured on failure
            Log.WriteLine($"[DIAG] Publish exit code: {result.ExitCode}");
            if (result.ExitCode != 0)
            {
                Log.WriteLine($"[DIAG] === PUBLISH FAILED - Full output captured above via MSBuild tracing ===");
            }

            result.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm, "Release");

            var blazorPublishDirectory = Path.Combine(publishDirectory.ToString(), "wwwroot");

            var expectedFiles = new[]
            {
                $"wwwroot/_framework/{WasmBootConfigFileName}",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.native.wasm",
                "wwwroot/_framework/blazorwasm.wasm",
                "wwwroot/_framework/System.Text.Json.wasm",
                "wwwroot/_content/RazorClassLibrary/wwwroot/exampleJsInterop.js",
                "wwwroot/_content/RazorClassLibrary/styles.css",
                "wwwroot/index.html",
                "wwwroot/js/LinkedScript.js",
                "wwwroot/blazorwasm.styles.css",
                "wwwroot/css/app.css",
                "web.config"
            };

            publishDirectory.Should().HaveFiles(expectedFiles);

            new FileInfo(Path.Combine(blazorPublishDirectory, "css", "app.css")).Should().Contain(".publish");
        }

        [Fact]
        public void AoT_Publish_WithExistingWebConfig_Works()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAssetWithAot(testAppName, new[] { "blazorwasm" })
                .WithProjectChanges((p, doc) =>
                {
                    var itemGroup = new XElement("PropertyGroup");
                    itemGroup.Add(new XElement("WasmFingerprintAssets", false));
                    doc.Root.Add(itemGroup);
                });

            var webConfigContents = "test webconfig contents";
            File.WriteAllText(Path.Combine(testInstance.TestRoot, "blazorwasm", "web.config"), webConfigContents);

            var publishCommand = new PublishCommand(testInstance, "blazorwasm");
            publishCommand.Execute("/p:Configuration=Release").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm, "Release");

            var webConfig = new BuildCommand(testInstance, "blazorwasm").GetOutputDirectory(configuration: "Release").File("web.config");

            // Verify web.config
            webConfig.Should().Exist();
            webConfig.Should().Contain(webConfigContents);
        }

        [Fact]
        public void AoT_Publish_HostedAppWithScopedCss_VisualStudio()
        {
            // Simulates publishing the same way VS does by setting BuildProjectReferences=false.
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAssetWithAot(testAppName, new[] { "blazorwasm", "blazorhosted" })
                .WithProjectChanges((p, doc) =>
                {
                    if (Path.GetFileName(p) == "blazorwasm.csproj")
                    {
                        var itemGroup = new XElement("PropertyGroup");
                        itemGroup.Add(new XElement("WasmFingerprintAssets", false));
                        doc.Root.Add(itemGroup);
                    }
                });

            File.WriteAllText(Path.Combine(testInstance.TestRoot, "blazorwasm", "App.razor.css"), "h1 { font-size: 16px; }");

            // VS builds projects individually and then a publish with BuildDependencies=false, but building the main project is a close enough approximation for this test.
            var buildCommand = CreateBuildCommand(testInstance, "blazorwasm");
            ExecuteCommand(buildCommand, "/p:BuildInsideVisualStudio=true", "/p:Configuration=Release").Should().Pass();

            // Publish
            var publishCommand = CreatePublishCommand(testInstance, "blazorhosted");
            ExecuteCommand(publishCommand, "/p:BuildProjectReferences=false", "/p:BuildInsideVisualStudio=true", "/p:Configuration=Release").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm, "Release");
            var blazorPublishDirectory = Path.Combine(publishDirectory.ToString(), "wwwroot");

            // Make sure the main project exists
            new FileInfo(Path.Combine(publishDirectory.ToString(), "blazorhosted.dll")).Should().Exist();

            // Verification for https://github.com/dotnet/aspnetcore/issues/19926. Verify binaries for projects
            // referenced by the Hosted project appear in the publish directory
            publishDirectory.Should().HaveFiles(new[]
            {
                "RazorClassLibrary.dll",
                "blazorwasm.dll"
            });

            publishDirectory.Should().HaveFiles(new[]
            {
                $"wwwroot/_framework/{WasmBootConfigFileName}",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.native.wasm",
                "wwwroot/_framework/blazorwasm.wasm",
                "wwwroot/_framework/System.Text.Json.wasm"
            });

            // Verify project references appear as static web assets
            // Also verify project references to the server project appear in the publish output
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/RazorClassLibrary.wasm",
                "RazorClassLibrary.dll"
            });

            // Verify static assets are in the publish directory
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/index.html"
            });

            // Verify scoped css
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/blazorwasm.styles.css"
            });

            // Verify static web assets from referenced projects are copied.
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_content/RazorClassLibrary/wwwroot/exampleJsInterop.js",
                "wwwroot/_content/RazorClassLibrary/styles.css",
            });

            // Verify web.config
            publishDirectory.Should().HaveFiles(new[]
            {
                "web.config"
            });

            VerifyBootManifestHashes(testInstance, Path.Combine(publishDirectory.ToString(), "wwwroot"));

            // Verify compression works
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/dotnet.native.wasm.br",
                "wwwroot/_framework/blazorwasm.wasm.br",
                "wwwroot/_framework/RazorClassLibrary.wasm.br",
                "wwwroot/_framework/System.Text.Json.wasm.br"
            });

            VerifyBootManifestHashes(testInstance, blazorPublishDirectory);
            VerifyServiceWorkerFiles(testInstance, blazorPublishDirectory,
                serviceWorkerPath: Path.Combine("serviceworkers", "my-service-worker.js"),
                serviceWorkerContent: "// This is the production service worker",
                assetsManifestPath: "custom-service-worker-assets.js");
        }

        private TestAsset CreateAspNetSdkTestAssetWithAot(
            string testAsset,
            string[] projectsToAoT,
            [CallerMemberName] string callerName = "")
        {
            return CreateAspNetSdkTestAsset(testAsset, callerName: callerName, identifier: "AoT")
                    .WithProjectChanges((project, document) =>
                    {
                        if (projectsToAoT.Contains(Path.GetFileNameWithoutExtension(project)))
                        {
                            document.Descendants("PropertyGroup").First().Add(new XElement("RunAoTCompilation", "true"));

                            foreach (var item in document.Descendants("PackageReference"))
                            {
                                item.SetAttributeValue("Version", "6.0.0");
                            }
                        }
                    });
        }
    }
}
