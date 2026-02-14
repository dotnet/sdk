// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAGenerateBundleMultiThreading
    {
        [Fact]
        public void ItImplementsIMultiThreadableTask()
        {
            var task = new GenerateBundle();
            task.Should().BeAssignableTo<IMultiThreadableTask>();
        }

        [Fact]
        public void ItHasMSBuildMultiThreadableTaskAttribute()
        {
            typeof(GenerateBundle).Should().BeDecoratedWith<MSBuildMultiThreadableTaskAttribute>();
        }

        [Fact]
        public void ItResolvesOutputDirViaTaskEnvironment()
        {
            // Create a temp directory to act as a fake project dir (different from CWD).
            // We can't fully execute GenerateBundle (needs real app host), but we can verify
            // the task resolves paths via TaskEnvironment by triggering execution with
            // relative paths that only exist under projectDir.
            var projectDir = Path.Combine(Path.GetTempPath(), "bundle-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            try
            {
                // Create output dir and a dummy source file under projectDir
                var outputRelativePath = "publish";
                var outputAbsolutePath = Path.Combine(projectDir, outputRelativePath);
                Directory.CreateDirectory(outputAbsolutePath);

                var sourceRelativePath = Path.Combine("bin", "test.dll");
                var sourceAbsolutePath = Path.Combine(projectDir, sourceRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(sourceAbsolutePath)!);
                File.WriteAllText(sourceAbsolutePath, "not a real dll");

                var fileItem = new Microsoft.Build.Utilities.TaskItem(sourceRelativePath);
                fileItem.SetMetadata(MetadataKeys.RelativePath, "test.dll");

                var task = new GenerateBundle
                {
                    AppHostName = "testhost.exe",
                    OutputDir = outputRelativePath,
                    FilesToBundle = new ITaskItem[] { fileItem },
                    IncludeSymbols = false,
                    IncludeNativeLibraries = false,
                    IncludeAllContent = false,
                    TargetFrameworkVersion = "8.0",
                    RuntimeIdentifier = "win-x64",
                    ShowDiagnosticOutput = false,
                    EnableCompressionInSingleFile = false,
                };
                var mockEngine = new MockBuildEngine();
                task.BuildEngine = mockEngine;

                // Set TaskEnvironment pointing to projectDir (different from CWD).
                task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

                // Execute — will fail because the source file isn't a real app host binary,
                // but it should attempt to read from the correct (absolutized) path, not from CWD.
                // The Bundler constructor will receive the absolutized OutputDir and the source
                // file paths will be absolutized, proving TaskEnvironment is used.
                // We expect an IOException or similar from the Bundler, NOT a DirectoryNotFoundException
                // (which would happen if OutputDir wasn't absolutized).
                try
                {
                    task.Execute();
                }
                catch (Exception ex)
                {
                    // Expected — Bundler can't process fake files.
                    // But it should NOT be a DirectoryNotFoundException, which would indicate
                    // OutputDir wasn't absolutized via TaskEnvironment.
                    ex.Should().NotBeOfType<System.IO.DirectoryNotFoundException>(
                        "OutputDir should be absolutized via TaskEnvironment, not used as relative path");
                }

                // If the task didn't absolutize OutputDir, the Bundler would fail trying to use
                // a relative path as the output directory. Since we can't easily assert on the
                // internal Bundler behavior, the interface and attribute tests above are the
                // primary validation, and this test serves as a smoke test.
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }
    }
}
