// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.NET.Build.Tests
{
    /// <summary>
    /// Tests for WASM workload detection with projects using Microsoft.NET.Sdk (not Blazor/WebAssembly SDK).
    /// These tests verify the fix for https://github.com/dotnet/sdk/issues/XXXXX where BenchmarkDotNet
    /// generated WASM projects stopped working because workload wasn't triggered for non-BlazorOrWasmSdk Exe projects.
    /// </summary>
    public class WasmWorkloadDetectionTests : SdkTest
    {
        public WasmWorkloadDetectionTests(ITestOutputHelper log) : base(log)
        {
        }

        /// <summary>
        /// Verifies that a console app targeting browser-wasm with plain Microsoft.NET.Sdk
        /// correctly triggers the WASM workload and sets UsingBrowserRuntimeWorkload=true.
        /// This is the scenario used by BenchmarkDotNet for WASM benchmarks.
        /// </summary>
        [Fact]
        public void WasmExeProjectWithoutBlazorSdk_RequiresWorkload()
        {
            var testProject = new TestProject()
            {
                Name = "WasmConsoleApp",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                RuntimeIdentifier = "browser-wasm"
            };

            testProject.SourceFiles["Program.cs"] = @"
using System;
Console.WriteLine(""Hello, WebAssembly!"");
";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            // Verify UsingBrowserRuntimeWorkload is set to true for Exe projects
            var getValuesCommand = new GetValuesCommand(testAsset, "UsingBrowserRuntimeWorkload")
            {
                DependsOnTargets = "ResolveFrameworkReferences",
                ShouldRestore = true
            };

            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            var values = getValuesCommand.GetValues();
            values.Should().Contain("true", "because Exe projects targeting browser-wasm should use the WASM workload");
        }

        /// <summary>
        /// Verifies that a library project targeting browser-wasm does NOT require the WASM workload.
        /// This preserves the behavior from runtime PR #122607 that allows library mode without workload.
        /// </summary>
        [Fact]
        public void WasmLibraryProjectWithoutBlazorSdk_DoesNotRequireWorkload()
        {
            var testProject = new TestProject()
            {
                Name = "WasmLibrary",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = false, // Library project
                RuntimeIdentifier = "browser-wasm"
            };

            testProject.SourceFiles["Class1.cs"] = @"
namespace WasmLibrary;
public class Class1 { }
";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            // Verify _WasmNativeWorkloadNeeded is NOT true for library projects (unless they set other workload-requiring properties)
            var getValuesCommand = new GetValuesCommand(testAsset, "_WasmNativeWorkloadNeeded")
            {
                DependsOnTargets = "ResolveFrameworkReferences",
                ShouldRestore = true
            };

            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            var values = getValuesCommand.GetValues();
            // Library projects should not force the workload unless they use native AOT features
            values.Should().NotContain("true", "because library projects should not force the WASM workload by default");
        }

        /// <summary>
        /// Verifies that _RuntimePackInWorkloadVersionCurrent is available when the workload is loaded.
        /// This property is needed by projects like BenchmarkDotNet that resolve the runtime pack manually.
        /// </summary>
        [Fact]
        public void WasmExeProject_HasRuntimePackVersion()
        {
            var testProject = new TestProject()
            {
                Name = "WasmConsoleApp",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                RuntimeIdentifier = "browser-wasm"
            };

            testProject.SourceFiles["Program.cs"] = @"
using System;
Console.WriteLine(""Hello, WebAssembly!"");
";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            // Verify _RuntimePackInWorkloadVersionCurrent is set (this comes from the workload manifest)
            var getValuesCommand = new GetValuesCommand(testAsset, "_RuntimePackInWorkloadVersionCurrent")
            {
                DependsOnTargets = "ResolveFrameworkReferences",
                ShouldRestore = true
            };

            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            var values = getValuesCommand.GetValues();
            values.Should().HaveCountGreaterThan(0, "because the workload should provide the runtime pack version");
            values.First().Should().NotBeNullOrEmpty("because _RuntimePackInWorkloadVersionCurrent should have a value");
        }
    }
}
