// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAProcessFrameworkReferencesMultiThreading
    {
        private const string MinimalRuntimeGraph = """
            {
                "runtimes": {
                    "any": { "#import": ["base"] },
                    "base": { "#import": [] }
                }
            }
            """;

        // Migration concern: GetPackPath calls Directory.Exists. Under multithreaded execution
        // the process CWD is not the project directory, so a relative TargetingPackRoot must be
        // resolved via TaskEnvironment. The metadata emitted to MSBuild must still be the
        // original (relative) form — absolutization must not leak into outputs (Sin 1).
        [Fact]
        public void It_resolves_relative_TargetingPackRoot_and_keeps_metadata_in_original_form()
        {
            using var env = new TaskTestEnvironment();

            const string packageId = "Microsoft.NETCore.App.Ref";
            const string version = "5.0.0";
            const string relativeRoot = "packs";

            // Place the targeting pack on disk under the project directory.
            env.CreateProjectDirectory(Path.Combine(relativeRoot, packageId, version));

            var runtimeGraphPath = Path.GetTempFileName();
            File.WriteAllText(runtimeGraphPath, MinimalRuntimeGraph);

            try
            {
                var task = new ProcessFrameworkReferences
                {
                    BuildEngine = new MockNeverCacheBuildEngine4(),
                    TaskEnvironment = env.TaskEnvironment,
                    TargetFrameworkIdentifier = ".NETCoreApp",
                    TargetFrameworkVersion = "5.0",
                    RuntimeGraphPath = runtimeGraphPath,
                    NETCoreSdkRuntimeIdentifier = "win-x64",
                    EnableTargetingPackDownload = true,
                    TargetingPackRoot = relativeRoot,
                    FrameworkReferences = new[]
                    {
                        new MockTaskItem(packageId, new Dictionary<string, string>())
                    },
                    KnownFrameworkReferences = new[] { CreateKnownTargetingPack(packageId, version) },
                };

                task.Execute().Should().BeTrue();

                task.TargetingPacks.Should().NotBeNull().And.HaveCount(1);
                var pack = task.TargetingPacks[0];

                // Resolution succeeded despite the process CWD pointing at SpawnDirectory ⇒
                // Directory.Exists was evaluated against ProjectDirectory via TaskEnvironment.
                // Metadata must be the original relative form, not an absolutized leak.
                var expectedRelativePath = Path.Combine(relativeRoot, packageId, version);
                pack.GetMetadata(MetadataKeys.Path).Should().Be(expectedRelativePath);
                pack.GetMetadata(MetadataKeys.PackageDirectory).Should().Be(expectedRelativePath);
            }
            finally
            {
                try { File.Delete(runtimeGraphPath); } catch { }
            }
        }

        // Migration concern: GetPackFolders reads DOTNETSDK_WORKLOAD_PACK_ROOTS. Under
        // multithreaded execution this must be read from the TaskEnvironment snapshot, not the
        // live process environment. The snapshot value below is independent of process state.
        [Fact]
        public void It_reads_workload_pack_roots_from_TaskEnvironment_snapshot()
        {
            using var env = new TaskTestEnvironment();

            const string packageId = "Microsoft.NETCore.App.Ref";
            const string version = "5.0.0";
            const string envRootName = "customRoot";

            // Pack lives only under the env-var-supplied root, never under TargetingPackRoot.
            env.CreateProjectDirectory(Path.Combine(envRootName, "packs", packageId, version));

            var envRootAbsolute = Path.Combine(env.ProjectDirectory, envRootName);
            var snapshot = new Dictionary<string, string>
            {
                { "DOTNETSDK_WORKLOAD_PACK_ROOTS", envRootAbsolute }
            };
            var taskEnv = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(env.ProjectDirectory, snapshot);

            var runtimeGraphPath = Path.GetTempFileName();
            File.WriteAllText(runtimeGraphPath, MinimalRuntimeGraph);

            try
            {
                var task = new ProcessFrameworkReferences
                {
                    BuildEngine = new MockNeverCacheBuildEngine4(),
                    TaskEnvironment = taskEnv,
                    TargetFrameworkIdentifier = ".NETCoreApp",
                    TargetFrameworkVersion = "5.0",
                    RuntimeGraphPath = runtimeGraphPath,
                    NETCoreSdkRuntimeIdentifier = "win-x64",
                    EnableTargetingPackDownload = false, // resolution must come from env var, not download fallback
                    // TargetingPackRoot intentionally unset — only the env var can drive resolution.
                    FrameworkReferences = new[]
                    {
                        new MockTaskItem(packageId, new Dictionary<string, string>())
                    },
                    KnownFrameworkReferences = new[] { CreateKnownTargetingPack(packageId, version) },
                };

                task.Execute().Should().BeTrue();

                task.TargetingPacks.Should().NotBeNull().And.HaveCount(1);
                var pack = task.TargetingPacks[0];

                // Resolution succeeded ⇒ env var was read from the TaskEnvironment snapshot.
                // Metadata is the original Path.Combine(envVar, "packs", id, version) form.
                var expectedPath = Path.Combine(envRootAbsolute, "packs", packageId, version);
                pack.GetMetadata(MetadataKeys.Path).Should().Be(expectedPath);
                pack.GetMetadata(MetadataKeys.PackageDirectory).Should().Be(expectedPath);
            }
            finally
            {
                try { File.Delete(runtimeGraphPath); } catch { }
            }
        }

        private static MockTaskItem CreateKnownTargetingPack(string packageId, string version)
        {
            return new MockTaskItem(packageId, new Dictionary<string, string>
            {
                { "TargetFramework", "net5.0" },
                { "RuntimeFrameworkName", packageId },
                { "DefaultRuntimeFrameworkVersion", version },
                { "LatestRuntimeFrameworkVersion", version },
                { "TargetingPackName", packageId },
                { "TargetingPackVersion", version },
            });
        }
    }
}
