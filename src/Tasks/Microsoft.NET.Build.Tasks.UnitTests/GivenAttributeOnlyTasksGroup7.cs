// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    /// <summary>
    /// Behavioral tests for attribute-only tasks in merge-group-7.
    /// These tasks received only the [MSBuildMultiThreadableTask] attribute — no source
    /// code changes — so we verify they still produce correct results.
    /// </summary>
    [Collection("CWD-Dependent")]

    public class GivenAttributeOnlyTasksGroup7
    {
        #region Attribute Presence

        [Fact]
        public void SelectRuntimeIdentifierSpecificItems_HasMultiThreadableAttribute()
        {
            typeof(SelectRuntimeIdentifierSpecificItems).GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>()
                .Should().NotBeNull("task must be decorated with [MSBuildMultiThreadableTask]");
        }

        [Fact]
        public void SetGeneratedAppConfigMetadata_HasMultiThreadableAttribute()
        {
            typeof(SetGeneratedAppConfigMetadata).GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>()
                .Should().NotBeNull("task must be decorated with [MSBuildMultiThreadableTask]");
        }

        [Fact]
        public void ValidateExecutableReferences_HasMultiThreadableAttribute()
        {
            typeof(ValidateExecutableReferences).GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>()
                .Should().NotBeNull("task must be decorated with [MSBuildMultiThreadableTask]");
        }

        [Fact]
        public void RemoveDuplicatePackageReferences_HasMultiThreadableAttribute()
        {
            typeof(RemoveDuplicatePackageReferences).GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>()
                .Should().NotBeNull("task must be decorated with [MSBuildMultiThreadableTask]");
        }

        #endregion

        #region SelectRuntimeIdentifierSpecificItems (parity test)

        [Fact]
        public void SelectRuntimeIdentifierSpecificItems_ProducesSameResultsRegardlessOfCwd()
        {
            // This parity test verifies the task behaves identically
            // when CWD is projectDir vs a different directory.
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"select-rid-parity-{Guid.NewGuid():N}"));
            var otherDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"select-rid-decoy-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(otherDir);
            var savedCwd = Directory.GetCurrentDirectory();
            try
            {
                var runtimeGraphJson = @"{
                    ""runtimes"": {
                        ""linux"": {},
                        ""linux-x64"": { ""#import"": [""linux""] },
                        ""win"": {},
                        ""win-x64"": { ""#import"": [""win""] }
                    }
                }";
                var graphPath = Path.Combine(projectDir, "runtime.json");
                File.WriteAllText(graphPath, runtimeGraphJson);

                var items = new[]
                {
                    CreateItemWithRid("Item1", "linux-x64"),
                    CreateItemWithRid("Item2", "win-x64"),
                    CreateItemWithRid("Item3", "linux")
                };

                // --- Run with CWD = projectDir ---
                Directory.SetCurrentDirectory(projectDir);
                var (cwdResult, cwdSelected) = RunSelectRidTask("linux-x64", items, graphPath, projectDir);

                // --- Run with CWD = otherDir ---
                Directory.SetCurrentDirectory(otherDir);
                var (otherResult, otherSelected) = RunSelectRidTask("linux-x64", items, graphPath, projectDir);

                cwdResult.Should().Be(otherResult, "task should return same result regardless of CWD");
                cwdSelected.Length.Should().Be(otherSelected.Length, "same number of items should be selected");

                // Both should select linux-x64 and linux, but NOT win-x64
                cwdSelected.Should().HaveCount(2);
                cwdSelected.Should().Contain(i => i.ItemSpec == "Item1"); // linux-x64
                cwdSelected.Should().Contain(i => i.ItemSpec == "Item3"); // linux
            }
            finally
            {
                Directory.SetCurrentDirectory(savedCwd);
                Directory.Delete(projectDir, true);
                if (Directory.Exists(otherDir)) Directory.Delete(otherDir, true);
            }
        }

        private static (bool result, ITaskItem[] selected) RunSelectRidTask(
            string targetRid, ITaskItem[] items, string graphPath, string projectDir)
        {
            var task = new SelectRuntimeIdentifierSpecificItems
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                TargetRuntimeIdentifier = targetRid,
                Items = items,
                RuntimeIdentifierGraphPath = graphPath
            };
            var result = task.Execute();
            return (result, task.SelectedItems ?? Array.Empty<ITaskItem>());
        }

        private static TaskItem CreateItemWithRid(string itemSpec, string rid)
        {
            var item = new TaskItem(itemSpec);
            item.SetMetadata("RuntimeIdentifier", rid);
            return item;
        }

        #endregion

        #region SetGeneratedAppConfigMetadata

        [Fact]
        public void SetGeneratedAppConfigMetadata_WithNoSourceAppConfig_SetsTargetPath()
        {
            var task = new SetGeneratedAppConfigMetadata
            {
                BuildEngine = new MockBuildEngine(),
                GeneratedAppConfigFile = "obj/myapp.exe.config",
                TargetName = "myapp.exe.config"
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.OutputAppConfigFileWithMetadata.Should().NotBeNull();
            task.OutputAppConfigFileWithMetadata.ItemSpec.Should().Be("obj/myapp.exe.config");
            task.OutputAppConfigFileWithMetadata.GetMetadata("TargetPath").Should().Be("myapp.exe.config");
        }

        [Fact]
        public void SetGeneratedAppConfigMetadata_WithSourceAppConfig_CopiesMetadata()
        {
            var sourceConfig = new MockTaskItem("app.config", new Dictionary<string, string>
            {
                { "Link", "linked/app.config" },
                { "TargetPath", "myapp.exe.config" }
            });

            var task = new SetGeneratedAppConfigMetadata
            {
                BuildEngine = new MockBuildEngine(),
                AppConfigFile = sourceConfig,
                GeneratedAppConfigFile = "obj/myapp.exe.config",
                TargetName = "myapp.exe.config"
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.OutputAppConfigFileWithMetadata.Should().NotBeNull();
            task.OutputAppConfigFileWithMetadata.ItemSpec.Should().Be("obj/myapp.exe.config");
            // Source metadata should be copied to the output
            task.OutputAppConfigFileWithMetadata.GetMetadata("TargetPath").Should().Be("myapp.exe.config");
        }

        [Fact]
        public void SetGeneratedAppConfigMetadata_OutputItemSpecIsGeneratedPath()
        {
            var task = new SetGeneratedAppConfigMetadata
            {
                BuildEngine = new MockBuildEngine(),
                GeneratedAppConfigFile = "generated/config.xml",
                TargetName = "output.exe.config"
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.OutputAppConfigFileWithMetadata.ItemSpec.Should().Be("generated/config.xml",
                "the output item should use the generated file path as its ItemSpec");
        }

        #endregion

        #region ValidateExecutableReferences

        [Fact]
        public void ValidateExecutableReferences_NonExecutableProject_SkipsValidation()
        {
            var task = new ValidateExecutableReferences
            {
                BuildEngine = new MockBuildEngine(),
                IsExecutable = false,
                SelfContained = true,
                ReferencedProjects = new ITaskItem[]
                {
                    new TaskItem("SomeProject.csproj")
                }
            };

            var result = task.Execute();

            result.Should().BeTrue("non-executable projects should skip validation entirely");
        }

        [Fact]
        public void ValidateExecutableReferences_NoReferencedProjects_Succeeds()
        {
            var task = new ValidateExecutableReferences
            {
                BuildEngine = new MockBuildEngine(),
                IsExecutable = true,
                SelfContained = false,
                ReferencedProjects = Array.Empty<ITaskItem>()
            };

            var result = task.Execute();

            result.Should().BeTrue("no references means nothing to validate");
        }

        [Fact]
        public void ValidateExecutableReferences_ProjectWithoutNearestTfm_SkipsProject()
        {
            // Projects without NearestTargetFramework metadata (e.g., C++ projects)
            // should be silently skipped
            var project = new MockTaskItem("NativeProject.vcxproj", new Dictionary<string, string>());

            var task = new ValidateExecutableReferences
            {
                BuildEngine = new MockBuildEngine(),
                IsExecutable = true,
                SelfContained = false,
                ReferencedProjects = new ITaskItem[] { project }
            };

            var result = task.Execute();

            result.Should().BeTrue("projects without NearestTargetFramework should be skipped");
        }

        #endregion

        #region RemoveDuplicatePackageReferences

        [Fact]
        public void RemoveDuplicatePackageReferences_RemovesDuplicates()
        {
            var packages = new ITaskItem[]
            {
                new MockTaskItem("MyPackage", new Dictionary<string, string> { { "Version", "1.0.0" } }),
                new MockTaskItem("MyPackage", new Dictionary<string, string> { { "Version", "1.0.0" } }),
                new MockTaskItem("OtherPackage", new Dictionary<string, string> { { "Version", "2.0.0" } })
            };

            var task = new RemoveDuplicatePackageReferences
            {
                BuildEngine = new MockBuildEngine(),
                InputPackageReferences = packages
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.UniquePackageReferences.Should().HaveCount(2);
            task.UniquePackageReferences.Should().Contain(i => i.ItemSpec == "MyPackage");
            task.UniquePackageReferences.Should().Contain(i => i.ItemSpec == "OtherPackage");
        }

        [Fact]
        public void RemoveDuplicatePackageReferences_PreservesVersionMetadata()
        {
            var packages = new ITaskItem[]
            {
                new MockTaskItem("MyPackage", new Dictionary<string, string> { { "Version", "3.5.1" } })
            };

            var task = new RemoveDuplicatePackageReferences
            {
                BuildEngine = new MockBuildEngine(),
                InputPackageReferences = packages
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.UniquePackageReferences.Should().HaveCount(1);
            task.UniquePackageReferences[0].GetMetadata("Version").Should().Be("3.5.1");
        }

        [Fact]
        public void RemoveDuplicatePackageReferences_DifferentVersionsAreNotDuplicates()
        {
            var packages = new ITaskItem[]
            {
                new MockTaskItem("MyPackage", new Dictionary<string, string> { { "Version", "1.0.0" } }),
                new MockTaskItem("MyPackage", new Dictionary<string, string> { { "Version", "2.0.0" } })
            };

            var task = new RemoveDuplicatePackageReferences
            {
                BuildEngine = new MockBuildEngine(),
                InputPackageReferences = packages
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.UniquePackageReferences.Should().HaveCount(2,
                "same package with different versions are distinct identities");
        }

        [Fact]
        public void RemoveDuplicatePackageReferences_SinglePackage_PassesThrough()
        {
            var packages = new ITaskItem[]
            {
                new MockTaskItem("SinglePackage", new Dictionary<string, string> { { "Version", "1.0.0" } })
            };

            var task = new RemoveDuplicatePackageReferences
            {
                BuildEngine = new MockBuildEngine(),
                InputPackageReferences = packages
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.UniquePackageReferences.Should().HaveCount(1);
            task.UniquePackageReferences[0].ItemSpec.Should().Be("SinglePackage");
        }

        #endregion

        // FilterResolvedFiles parity test removed — it belongs in GivenAFilterResolvedFilesMultiThreading.cs
        // (FilterResolvedFiles is Pattern B, not attribute-only). The duplicate here also had a bug:
        // it created FilterResolvedFiles without setting TaskEnvironment, causing NullReferenceException.

        #region Concurrent Execution

        [Theory]
        [InlineData(4)]
        [InlineData(16)]
        public async System.Threading.Tasks.Task SelectRuntimeIdentifierSpecificItems_ConcurrentExecution(int parallelism)
        {
            var projectDir = Path.Combine(Path.GetTempPath(), $"select-rid-concurrent-{Guid.NewGuid():N}");
            Directory.CreateDirectory(projectDir);
            var runtimeGraphPath = Path.Combine(projectDir, "runtime.json");
            File.WriteAllText(runtimeGraphPath, @"{ ""runtimes"": { ""linux-x64"": { ""#import"": [""linux"", ""unix""] } } }");
            try
            {
            var errors = new ConcurrentBag<string>();
            using var startGate = new ManualResetEventSlim(false);
            var tasks = new System.Threading.Tasks.Task[parallelism];
            for (int i = 0; i < parallelism; i++)
            {
                int idx = i;
                tasks[idx] = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var task = new SelectRuntimeIdentifierSpecificItems
                    {
                        BuildEngine = new MockBuildEngine(),
                        TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                        TargetRuntimeIdentifier = "linux-x64",
                        Items = new ITaskItem[]
                        {
                            CreateItemWithRid($"Item{idx}", "linux-x64")
                        },
                        RuntimeIdentifierGraphPath = "runtime.json"
                    };
                    startGate.Wait();
                    var result = task.Execute();
                    if (!result) errors.Add($"Thread {idx}: Execute returned false");
                }
                catch (Exception ex) { errors.Add($"Thread {idx}: {ex.Message}"); }
            });
            }
            startGate.Set();
            await System.Threading.Tasks.Task.WhenAll(tasks);

            errors.Should().BeEmpty();
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        [Theory]
        [InlineData(4)]
        [InlineData(16)]
        public async System.Threading.Tasks.Task SetGeneratedAppConfigMetadata_ConcurrentExecution(int parallelism)
        {
            var errors = new ConcurrentBag<string>();
            using var startGate = new ManualResetEventSlim(false);
            var tasks = new System.Threading.Tasks.Task[parallelism];
            for (int i = 0; i < parallelism; i++)
            {
                int idx = i;
                tasks[idx] = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var task = new SetGeneratedAppConfigMetadata
                    {
                        BuildEngine = new MockBuildEngine(),
                        GeneratedAppConfigFile = $"obj/app{idx}.exe.config",
                        TargetName = $"app{idx}.exe.config"
                    };
                    startGate.Wait();
                    task.Execute();
                }
                catch (Exception ex) { errors.Add($"Thread {idx}: {ex.Message}"); }
            });
            }
            startGate.Set();
            await System.Threading.Tasks.Task.WhenAll(tasks);

            errors.Should().BeEmpty();
        }

        [Theory]
        [InlineData(4)]
        [InlineData(16)]
        public async System.Threading.Tasks.Task ValidateExecutableReferences_ConcurrentExecution(int parallelism)
        {
            var errors = new ConcurrentBag<string>();
            using var startGate = new ManualResetEventSlim(false);
            var tasks = new System.Threading.Tasks.Task[parallelism];
            for (int i = 0; i < parallelism; i++)
            {
                int idx = i;
                tasks[idx] = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var task = new ValidateExecutableReferences
                    {
                        BuildEngine = new MockBuildEngine(),
                        IsExecutable = false,
                        SelfContained = false,
                        ReferencedProjects = Array.Empty<ITaskItem>()
                    };
                    startGate.Wait();
                    task.Execute();
                }
                catch (Exception ex) { errors.Add($"Thread {idx}: {ex.Message}"); }
            });
            }
            startGate.Set();
            await System.Threading.Tasks.Task.WhenAll(tasks);

            errors.Should().BeEmpty();
        }

        [Theory]
        [InlineData(4)]
        [InlineData(16)]
        public async System.Threading.Tasks.Task RemoveDuplicatePackageReferences_ConcurrentExecution(int parallelism)
        {
            var errors = new ConcurrentBag<string>();
            using var startGate = new ManualResetEventSlim(false);
            var tasks = new System.Threading.Tasks.Task[parallelism];
            for (int i = 0; i < parallelism; i++)
            {
                int idx = i;
                tasks[idx] = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var task = new RemoveDuplicatePackageReferences
                    {
                        BuildEngine = new MockBuildEngine(),
                        InputPackageReferences = new ITaskItem[]
                        {
                            new MockTaskItem($"Package{idx}", new Dictionary<string, string> { { "Version", "1.0.0" } })
                        }
                    };
                    startGate.Wait();
                    task.Execute();
                }
                catch (Exception ex) { errors.Add($"Thread {idx}: {ex.Message}"); }
            });
            }
            startGate.Set();
            await System.Threading.Tasks.Task.WhenAll(tasks);

            errors.Should().BeEmpty();
        }

        #endregion
    }
}
