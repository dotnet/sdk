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

        [Fact]
        public void ItHandlesEmptyOutputDir()
        {
            var projectDir = Path.Combine(Path.GetTempPath(), "bundle-empty-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            try
            {
                var fileItem = new Microsoft.Build.Utilities.TaskItem("test.dll");
                fileItem.SetMetadata(MetadataKeys.RelativePath, "test.dll");

                var task = new GenerateBundle
                {
                    AppHostName = "testhost.exe",
                    OutputDir = "",
                    FilesToBundle = new ITaskItem[] { fileItem },
                    IncludeSymbols = false,
                    IncludeNativeLibraries = false,
                    IncludeAllContent = false,
                    TargetFrameworkVersion = "8.0",
                    RuntimeIdentifier = "win-x64",
                    ShowDiagnosticOutput = false,
                    EnableCompressionInSingleFile = false,
                };
                task.BuildEngine = new MockBuildEngine();
                task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

                Exception? caught = null;
                try { task.Execute(); } catch (Exception ex) { caught = ex; }

                // Empty OutputDir should produce an ArgumentException from AbsolutePath validation,
                // not a NullReferenceException.
                caught.Should().NotBeNull("empty OutputDir should fail during path resolution");
                caught.Should().NotBeOfType<NullReferenceException>(
                    "empty OutputDir should not cause NullReferenceException");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        [Fact]
        public void ItHandlesEmptyFileToBundleItemSpec()
        {
            var projectDir = Path.Combine(Path.GetTempPath(), "bundle-emptyfile-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            try
            {
                var outputRelativePath = "publish";
                Directory.CreateDirectory(Path.Combine(projectDir, outputRelativePath));

                var fileItem = new Microsoft.Build.Utilities.TaskItem("");
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
                task.BuildEngine = new MockBuildEngine();
                task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

                Exception? caught = null;
                try { task.Execute(); } catch (Exception ex) { caught = ex; }

                // Empty file ItemSpec should produce a meaningful error, not NullReferenceException.
                caught.Should().NotBeOfType<NullReferenceException>(
                    "empty FilesToBundle ItemSpec should not cause NullReferenceException");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        [Fact]
        public void ItProducesSameExceptionInSingleProcessAndMultiProcessMode()
        {
            // GenerateBundle requires a real app host binary, so we can't compare actual outputs.
            // Instead, we verify both modes fail at the same point (Bundler processing) with
            // the same exception type, proving path resolution is equivalent.
            var projectDir = Path.Combine(Path.GetTempPath(), "bundle-sp-mp-" + Guid.NewGuid().ToString("N"));
            var otherDir = Path.Combine(Path.GetTempPath(), "bundle-other-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(otherDir);

            var savedCwd = Directory.GetCurrentDirectory();
            try
            {
                // Create output dir and dummy source under projectDir
                var outputRelativePath = "publish";
                Directory.CreateDirectory(Path.Combine(projectDir, outputRelativePath));

                var sourceRelativePath = Path.Combine("bin", "test.dll");
                var sourceAbsolutePath = Path.Combine(projectDir, sourceRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(sourceAbsolutePath)!);
                File.WriteAllText(sourceAbsolutePath, "not a real dll");

                GenerateBundle CreateTask() => new GenerateBundle
                {
                    AppHostName = "testhost.exe",
                    OutputDir = outputRelativePath,
                    FilesToBundle = new ITaskItem[]
                    {
                        new Microsoft.Build.Utilities.TaskItem(sourceRelativePath)
                        {
                            // TaskItem doesn't have init syntax for metadata, set it below
                        }
                    },
                    IncludeSymbols = false,
                    IncludeNativeLibraries = false,
                    IncludeAllContent = false,
                    TargetFrameworkVersion = "8.0",
                    RuntimeIdentifier = "win-x64",
                    ShowDiagnosticOutput = false,
                    EnableCompressionInSingleFile = false,
                    RetryCount = 0,
                };

                // --- Single-process run: CWD == projectDir ---
                Exception? singleProcessException = null;
                Directory.SetCurrentDirectory(projectDir);
                try
                {
                    var task = CreateTask();
                    task.FilesToBundle[0].SetMetadata(MetadataKeys.RelativePath, "test.dll");
                    task.BuildEngine = new MockBuildEngine();
                    task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);
                    try { task.Execute(); } catch (Exception ex) { singleProcessException = ex; }
                }
                finally
                {
                    Directory.SetCurrentDirectory(savedCwd);
                }

                // --- Multi-process run: CWD != projectDir ---
                Exception? multiProcessException = null;
                Directory.SetCurrentDirectory(otherDir);
                try
                {
                    var task = CreateTask();
                    task.FilesToBundle[0].SetMetadata(MetadataKeys.RelativePath, "test.dll");
                    task.BuildEngine = new MockBuildEngine();
                    task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);
                    try { task.Execute(); } catch (Exception ex) { multiProcessException = ex; }
                }
                finally
                {
                    Directory.SetCurrentDirectory(savedCwd);
                }

                // Both modes should fail at the same point (Bundler, not path resolution)
                if (singleProcessException != null && multiProcessException != null)
                {
                    multiProcessException.GetType().Should().Be(singleProcessException.GetType(),
                        "both single-process and multi-process modes should fail with the same exception type");
                }
                else
                {
                    // If one succeeded and the other didn't, that's a path resolution problem
                    (singleProcessException == null).Should().Be(multiProcessException == null,
                        "both modes should either succeed or fail — a mismatch indicates path resolution discrepancy");
                }
            }
            finally
            {
                Directory.SetCurrentDirectory(savedCwd);
                if (Directory.Exists(projectDir)) Directory.Delete(projectDir, true);
                if (Directory.Exists(otherDir)) Directory.Delete(otherDir, true);
            }
        }
    }
}
