// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAParseTargetManifestsMultiThreading
    {
        private const string ArtifactXml = """
            <StoreArtifacts>
              <Package Id="TestPackage" Version="1.0.0" />
            </StoreArtifacts>
            """;

        private static (bool result, MockBuildEngine engine, ITaskItem[]? packages) RunTask(
            string manifestRelPath, string projectDir)
        {
            var engine = new MockBuildEngine();
            var task = new ParseTargetManifests
            {
                BuildEngine = engine,
                TargetManifestFiles = manifestRelPath,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
            };

            var result = task.Execute();
            return (result, engine, task.RuntimeStorePackages);
        }

        [Fact]
        public void ManifestFile_IsResolvedRelativeToProjectDirectory()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"manifest-mt-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            try
            {
                File.WriteAllText(Path.Combine(projectDir, "artifact.xml"), ArtifactXml);

                var task = new ParseTargetManifests
                {
                    BuildEngine = new MockBuildEngine(),
                    TargetManifestFiles = "artifact.xml",
                };

                var teProp = task.GetType().GetProperty("TaskEnvironment");
                teProp.Should().NotBeNull("task must have a TaskEnvironment property after migration");
                teProp!.SetValue(task, TaskEnvironmentHelper.CreateForTest(projectDir));

                var result = task.Execute();
                result.Should().BeTrue("task should succeed when manifest is found via TaskEnvironment");
                task.RuntimeStorePackages.Should().HaveCount(1);
                task.RuntimeStorePackages[0].GetMetadata("NuGetPackageId").Should().Be("TestPackage");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        [Theory]
        [InlineData(4)]
        [InlineData(16)]
        public void ParseTargetManifests_ConcurrentExecution(int parallelism)
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"manifest-conc-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            try
            {
                File.WriteAllText(Path.Combine(projectDir, "artifact.xml"), ArtifactXml);

                var errors = new ConcurrentBag<string>();
                var barrier = new Barrier(parallelism);
                Parallel.For(0, parallelism, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, i =>
                {
                    try
                    {
                        var task = new ParseTargetManifests
                        {
                            BuildEngine = new MockBuildEngine(),
                            TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                            TargetManifestFiles = "artifact.xml",
                        };
                        barrier.SignalAndWait();
                        task.Execute();
                    }
                    catch (Exception ex) { errors.Add($"Thread {i}: {ex.Message}"); }
                });
                errors.Should().BeEmpty();
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        [Fact]
        public void ItProducesSameResultsInMultiProcessAndMultiThreadedEnvironments()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"manifest-parity-{Guid.NewGuid():N}"));
            var otherDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"manifest-decoy-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(otherDir);
            var savedCwd = Directory.GetCurrentDirectory();
            try
            {
                File.WriteAllText(Path.Combine(projectDir, "artifact.xml"), ArtifactXml);

                // --- Multiprocess mode: CWD == projectDir ---
                Directory.SetCurrentDirectory(projectDir);
                var (mpResult, mpEngine, mpPackages) = RunTask("artifact.xml", projectDir);

                // --- Multithreaded mode: CWD == otherDir ---
                Directory.SetCurrentDirectory(otherDir);
                var (mtResult, mtEngine, mtPackages) = RunTask("artifact.xml", projectDir);

                mpResult.Should().Be(mtResult,
                    "task should return the same success/failure in both environments");
                mpEngine.Errors.Count.Should().Be(mtEngine.Errors.Count,
                    "error count should be the same in both environments");
                for (int i = 0; i < mpEngine.Errors.Count; i++)
                {
                    mpEngine.Errors[i].Message.Should().Be(
                        mtEngine.Errors[i].Message,
                        $"error message [{i}] should be identical in both environments");
                }
                mpEngine.Warnings.Count.Should().Be(mtEngine.Warnings.Count,
                    "warning count should be the same in both environments");

                // Both should produce the same packages
                (mpPackages?.Length ?? 0).Should().Be(mtPackages?.Length ?? 0,
                    "package count should be the same in both environments");
                if (mpPackages != null && mtPackages != null)
                {
                    for (int i = 0; i < mpPackages.Length; i++)
                    {
                        mpPackages[i].GetMetadata("NuGetPackageId").Should().Be(
                            mtPackages[i].GetMetadata("NuGetPackageId"),
                            $"package [{i}] NuGetPackageId should be identical");
                    }
                }
            }
            finally
            {
                Directory.SetCurrentDirectory(savedCwd);
                Directory.Delete(projectDir, true);
                if (Directory.Exists(otherDir)) Directory.Delete(otherDir, true);
            }
        }
    }
}
