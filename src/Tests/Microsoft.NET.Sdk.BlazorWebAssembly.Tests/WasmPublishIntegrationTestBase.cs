// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Text.Json;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.NET.Sdk.BlazorWebAssembly.Tests.ServiceWorkerAssert;
using ResourceHashesByNameDictionary = System.Collections.Generic.Dictionary<string, string>;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public abstract class WasmPublishIntegrationTestBase : AspNetSdkTest
    {
        public WasmPublishIntegrationTestBase(ITestOutputHelper log) : base(log) { }

        protected void AssertRIDPublishOuput(PublishCommand command, TestAsset testInstance, bool hosted = false)
        {
            var publishDirectory = command.GetOutputDirectory(DefaultTfm, "Debug", "linux-x64");

            // Make sure the main project exists
            publishDirectory.Should().HaveFiles(new[]
            {
                "libhostfxr.so" // Verify that we're doing a self-contained deployment
            });

            publishDirectory.Should().HaveFiles(new[]
            {
                "RazorClassLibrary.dll",
                "blazorwasm.dll",
            });

            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.wasm",
                "wwwroot/_framework/blazorwasm.dll",
                "wwwroot/_framework/System.Text.Json.dll"
            });


            publishDirectory.Should().HaveFiles(new[]
            {
                // Verify project references appear as static web assets
                "wwwroot/_framework/RazorClassLibrary.dll",
                // Also verify project references to the server project appear in the publish output
                "RazorClassLibrary.dll",
            });

            // Verify static assets are in the publish directory
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/index.html"
            });

            // Verify static web assets from referenced projects are copied.
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_content/RazorClassLibrary/wwwroot/exampleJsInterop.js",
                "wwwroot/_content/RazorClassLibrary/styles.css",
            });

            if (!hosted)
            {
                // Verify web.config
                publishDirectory.Should().HaveFiles(new[]
                {
                    "web.config"
                });
            }

            VerifyBootManifestHashes(testInstance, Path.Combine(publishDirectory.ToString(), "wwwroot"));

            // Verify compression works
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/dotnet.wasm.br",
                "wwwroot/_framework/blazorwasm.dll.br",
                "wwwroot/_framework/RazorClassLibrary.dll.br",
                "wwwroot/_framework/System.Text.Json.dll.br"
            });
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/dotnet.wasm.gz",
                "wwwroot/_framework/blazorwasm.dll.gz",
                "wwwroot/_framework/RazorClassLibrary.dll.gz",
                "wwwroot/_framework/System.Text.Json.dll.gz"
            });

            VerifyServiceWorkerFiles(testInstance, Path.Combine(publishDirectory.ToString(), "wwwroot"),
                serviceWorkerPath: Path.Combine("serviceworkers", "my-service-worker.js"),
                serviceWorkerContent: "// This is the production service worker",
                assetsManifestPath: "custom-service-worker-assets.js");
        }

        protected static void VerifyBootManifestHashes(TestAsset testAsset, string blazorPublishDirectory)
        {
            var bootManifestResolvedPath = Path.Combine(blazorPublishDirectory, "_framework", "blazor.boot.json");
            var bootManifestJson = File.ReadAllText(bootManifestResolvedPath);
            var bootManifest = JsonSerializer.Deserialize<BootJsonData>(bootManifestJson);

            VerifyBootManifestHashes(testAsset, blazorPublishDirectory, bootManifest.resources.assembly);
            VerifyBootManifestHashes(testAsset, blazorPublishDirectory, bootManifest.resources.runtime);

            if (bootManifest.resources.pdb != null)
            {
                VerifyBootManifestHashes(testAsset, blazorPublishDirectory, bootManifest.resources.pdb);
            }

            if (bootManifest.resources.satelliteResources != null)
            {
                foreach (var resourcesForCulture in bootManifest.resources.satelliteResources.Values)
                {
                    VerifyBootManifestHashes(testAsset, blazorPublishDirectory, resourcesForCulture);
                }
            }

            static void VerifyBootManifestHashes(TestAsset testAsset, string blazorPublishDirectory, ResourceHashesByNameDictionary resources)
            {
                foreach (var (name, hash) in resources)
                {
                    var relativePath = Path.Combine(blazorPublishDirectory, "_framework", name);
                    new FileInfo(Path.Combine(testAsset.TestRoot, relativePath)).Should().HashEquals(ParseWebFormattedHash(hash));
                }
            }

            static string ParseWebFormattedHash(string webFormattedHash)
            {
                Assert.StartsWith("sha256-", webFormattedHash);
                return webFormattedHash.Substring(7);
            }
        }

        protected void VerifyTypeGranularTrimming(string blazorPublishDirectory)
        {
            VerifyAssemblyHasTypes(Path.Combine(blazorPublishDirectory, "_framework", "Microsoft.AspNetCore.Components.dll"), new[] {
                    "Microsoft.AspNetCore.Components.RouteView",
                    "Microsoft.AspNetCore.Components.RouteData",
                    "Microsoft.AspNetCore.Components.CascadingParameterAttribute"
                });
        }

        protected void VerifyAssemblyHasTypes(string assemblyPath, string[] expectedTypes)
        {
            new FileInfo(assemblyPath).Should().Exist();

            using (var file = File.OpenRead(assemblyPath))
            {
                using var peReader = new PEReader(file);
                var metadataReader = peReader.GetMetadataReader();
                var types = metadataReader.TypeDefinitions.Where(t => !t.IsNil).Select(t =>
                {
                    var type = metadataReader.GetTypeDefinition(t);
                    return metadataReader.GetString(type.Namespace) + "." + metadataReader.GetString(type.Name);
                }).ToArray();
                types.Should().Contain(expectedTypes);
            }
        }

        protected static BootJsonData ReadBootJsonData(string path)
        {
            return JsonSerializer.Deserialize<BootJsonData>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
    }
}
