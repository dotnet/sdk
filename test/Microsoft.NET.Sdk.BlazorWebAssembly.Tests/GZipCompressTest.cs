// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.IO.Compression;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Sdk.BlazorWebAssembly;
using Moq;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    // These tests exercise the multithreaded (TaskEnvironment-based) path of GZipCompress.
    // They deliberately do NOT mutate the process working directory: the natural process CWD
    // (the test host's output directory) acts as the "decoy" that does not contain the inputs,
    // while TaskEnvironment.ProjectDirectory points at a temp directory that does. If the task
    // resolved relative paths against the process CWD instead of the project directory, it would
    // fail to find the inputs and Execute() would return false.
    [TestClass]
    public class GZipCompressTest
    {
        [TestMethod]
        public void Compresses_ResolvingRelativePathsAgainstProjectDirectory_NotProcessCurrentDirectory()
        {
            var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(GZipCompressTest), Guid.NewGuid().ToString("N"));
            var projectDir = Path.Combine(testRoot, "project");
            var relativeInputPath = Path.Combine("wwwroot", "app.js");
            const string relativeOutputDirectory = "compressed";
            const string content = "// some javascript content that is long enough to be worth compressing\n";

            Directory.CreateDirectory(Path.Combine(projectDir, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDir, relativeInputPath), content);

            try
            {
                var task = CreateTask(projectDir, relativeInputPath, relativeOutputDirectory);

                task.Execute().Should().BeTrue();

                var projectOutputDir = Path.Combine(projectDir, relativeOutputDirectory);
                var compressedFiles = Directory.GetFiles(projectOutputDir, "*.gz");
                compressedFiles.Should().HaveCount(1, "the compressed output must be written under the project directory");
                Decompress(compressedFiles[0]).Should().Be(content);

                // The output item spec must remain the original relative form, not an absolutized path.
                task.CompressedFiles.Should().HaveCount(1);
                Path.IsPathRooted(task.CompressedFiles[0].ItemSpec).Should().BeFalse("the output item spec must not be absolutized");
                task.CompressedFiles[0].GetMetadata("RelativePath").Should().Be(relativeInputPath + ".gz");
                task.CompressedFiles[0].GetMetadata("OriginalItemSpec").Should().Be(relativeInputPath);
            }
            finally
            {
                if (Directory.Exists(testRoot))
                {
                    Directory.Delete(testRoot, recursive: true);
                }
            }
        }

        [TestMethod]
        public void Compresses_TwoInstancesWithDifferentProjectDirectories_AreIndependent()
        {
            var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(GZipCompressTest), Guid.NewGuid().ToString("N"));
            var relativeInputPath = Path.Combine("wwwroot", "app.js");
            const string relativeOutputDirectory = "compressed";

            var projectDirA = Path.Combine(testRoot, "a");
            var projectDirB = Path.Combine(testRoot, "b");
            const string contentA = "// content for project A, distinct from B\n";
            const string contentB = "// content for project B, distinct from A\n";

            Directory.CreateDirectory(Path.Combine(projectDirA, "wwwroot"));
            Directory.CreateDirectory(Path.Combine(projectDirB, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirA, relativeInputPath), contentA);
            File.WriteAllText(Path.Combine(projectDirB, relativeInputPath), contentB);

            try
            {
                var taskA = CreateTask(projectDirA, relativeInputPath, relativeOutputDirectory);
                var taskB = CreateTask(projectDirB, relativeInputPath, relativeOutputDirectory);

                taskA.Execute().Should().BeTrue();
                taskB.Execute().Should().BeTrue();

                // Each instance resolves the same relative input/output against its own project
                // directory, proving resolution is per-instance and not shared via process state.
                var outputA = Directory.GetFiles(Path.Combine(projectDirA, relativeOutputDirectory), "*.gz");
                var outputB = Directory.GetFiles(Path.Combine(projectDirB, relativeOutputDirectory), "*.gz");
                outputA.Should().HaveCount(1);
                outputB.Should().HaveCount(1);
                Decompress(outputA[0]).Should().Be(contentA);
                Decompress(outputB[0]).Should().Be(contentB);
            }
            finally
            {
                if (Directory.Exists(testRoot))
                {
                    Directory.Delete(testRoot, recursive: true);
                }
            }
        }

        private static GZipCompress CreateTask(string projectDir, string relativeInputPath, string relativeOutputDirectory)
        {
            var inputItem = new TaskItem(relativeInputPath);
            inputItem.SetMetadata("RelativePath", relativeInputPath);

            return new GZipCompress
            {
                BuildEngine = new Mock<IBuildEngine>().Object,
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
                FilesToCompress = [inputItem],
                OutputDirectory = relativeOutputDirectory,
            };
        }

        private static string Decompress(string gzipPath)
        {
            using var fileStream = File.OpenRead(gzipPath);
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            using var reader = new StreamReader(gzipStream);
            return reader.ReadToEnd();
        }
    }
}
