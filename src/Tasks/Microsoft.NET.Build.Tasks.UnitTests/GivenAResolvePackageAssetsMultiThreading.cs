// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAResolvePackageAssetsMultiThreading
    {
        [Fact]
        public void ItImplementsIMultiThreadableTask()
        {
            var task = new ResolvePackageAssets();
            task.Should().BeAssignableTo<IMultiThreadableTask>();
        }

        [Fact]
        public void ItHasMSBuildMultiThreadableTaskAttribute()
        {
            typeof(ResolvePackageAssets).Should().BeDecoratedWith<MSBuildMultiThreadableTaskAttribute>();
        }

        [Fact]
        public void ItHasTaskEnvironmentProperty()
        {
            var prop = typeof(ResolvePackageAssets).GetProperty("TaskEnvironment",
                BindingFlags.Public | BindingFlags.Instance);
            prop.Should().NotBeNull("ResolvePackageAssets must have a public TaskEnvironment property");
            prop!.PropertyType.Should().Be(typeof(TaskEnvironment));
            prop.CanRead.Should().BeTrue();
            prop.CanWrite.Should().BeTrue();
        }

        [Fact]
        public void ItResolvesCacheFilePathViaTaskEnvironment()
        {
            var projectDir = Path.Combine(Path.GetTempPath(), "rpa-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            try
            {
                var task = new ResolvePackageAssets();
                task.BuildEngine = new MockBuildEngine();

                // Set TaskEnvironment via reflection to work both before and after migration
                var teProp = typeof(ResolvePackageAssets).GetProperty("TaskEnvironment",
                    BindingFlags.Public | BindingFlags.Instance);
                teProp.Should().NotBeNull("ResolvePackageAssets must have TaskEnvironment property after migration");
                teProp!.SetValue(task, TaskEnvironmentHelper.CreateForTest(projectDir));

                var objDir = Path.Combine(projectDir, "obj");
                Directory.CreateDirectory(objDir);

                // Use relative path for cache file — migration must resolve via TaskEnvironment
                task.ProjectAssetsCacheFile = Path.Combine("obj", "project.assets.cache");
                task.ProjectAssetsFile = Path.Combine(objDir, "project.assets.json");
                task.ProjectPath = Path.Combine(projectDir, "test.csproj");
                task.TargetFramework = "net8.0";
                task.DotNetAppHostExecutableNameWithoutExtension = "apphost";
                task.DefaultImplicitPackages = "Microsoft.NETCore.App";

                var assetsJson = @"{
  ""version"": 3,
  ""targets"": {
    ""net8.0"": {}
  },
  ""libraries"": {},
  ""projectFileDependencyGroups"": {
    ""net8.0"": []
  },
  ""packageFolders"": {},
  ""project"": {
    ""version"": ""1.0.0"",
    ""restore"": {
      ""projectUniqueName"": ""test"",
      ""projectName"": ""test"",
      ""projectPath"": """ + Path.Combine(projectDir, "test.csproj").Replace("\\", "\\\\") + @""",
      ""packagesPath"": """ + Path.Combine(projectDir, "packages").Replace("\\", "\\\\") + @""",
      ""outputPath"": """ + objDir.Replace("\\", "\\\\") + @""",
      ""projectStyle"": ""PackageReference"",
      ""frameworks"": {
        ""net8.0"": {
          ""targetAlias"": ""net8.0""
        }
      }
    },
    ""frameworks"": {
      ""net8.0"": {}
    }
  }
}";
                File.WriteAllText(Path.Combine(objDir, "project.assets.json"), assetsJson);

                // Enable disk cache so that the code path using ProjectAssetsCacheFile is exercised
                task.DisablePackageAssetsCache = false;

                var result = task.Execute();

                result.Should().BeTrue("task should execute successfully");

                // The cache file should be created under projectDir, not under CWD
                var expectedCachePath = Path.Combine(projectDir, "obj", "project.assets.cache");
                File.Exists(expectedCachePath).Should().BeTrue(
                    "cache file should be written relative to ProjectDirectory, not CWD");
            }
            finally
            {
                try { Directory.Delete(projectDir, true); } catch { }
            }
        }

        [Fact]
        public void OutputMatchesBetweenSingleAndMultiProcessMode()
        {
            var projectDir = Path.Combine(Path.GetTempPath(), "rpa-parity-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            try
            {
                var objDir = Path.Combine(projectDir, "obj");
                Directory.CreateDirectory(objDir);
                var packagesDir = Path.Combine(projectDir, "packages");
                Directory.CreateDirectory(packagesDir);

                var assetsJson = @"{
  ""version"": 3,
  ""targets"": {
    ""net8.0"": {}
  },
  ""libraries"": {},
  ""projectFileDependencyGroups"": {
    ""net8.0"": []
  },
  ""packageFolders"": {},
  ""project"": {
    ""version"": ""1.0.0"",
    ""restore"": {
      ""projectUniqueName"": ""test"",
      ""projectName"": ""test"",
      ""projectPath"": """ + Path.Combine(projectDir, "test.csproj").Replace("\\", "\\\\") + @""",
      ""packagesPath"": """ + packagesDir.Replace("\\", "\\\\") + @""",
      ""outputPath"": """ + objDir.Replace("\\", "\\\\") + @""",
      ""projectStyle"": ""PackageReference"",
      ""frameworks"": {
        ""net8.0"": {
          ""targetAlias"": ""net8.0""
        }
      }
    },
    ""frameworks"": {
      ""net8.0"": {}
    }
  }
}";
                File.WriteAllText(Path.Combine(objDir, "project.assets.json"), assetsJson);

                // --- Run 1: Single-process mode (TaskEnvironment from CWD == projectDir) ---
                var singleTask = CreateResolvePackageAssetsTask(projectDir, objDir);
                singleTask.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);
                singleTask.DisablePackageAssetsCache = true;

                var singleResult = singleTask.Execute();
                singleResult.Should().BeTrue("single-process mode should succeed");

                // --- Run 2: Multi-process mode (explicit project directory == same dir) ---
                var multiTask = CreateResolvePackageAssetsTask(projectDir, objDir);
                multiTask.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);
                multiTask.DisablePackageAssetsCache = true;

                var multiResult = multiTask.Execute();
                multiResult.Should().BeTrue("multi-process mode should succeed");

                // Compare all [Output] properties via reflection
                var outputProperties = typeof(ResolvePackageAssets)
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetCustomAttribute<OutputAttribute>() != null)
                    .ToList();

                outputProperties.Should().NotBeEmpty("ResolvePackageAssets should have [Output] properties");

                foreach (var prop in outputProperties)
                {
                    var singleValue = prop.GetValue(singleTask);
                    var multiValue = prop.GetValue(multiTask);

                    if (singleValue is ITaskItem[] singleItems)
                    {
                        var multiItems = multiValue as ITaskItem[];
                        multiItems.Should().NotBeNull($"[Output] {prop.Name} should be ITaskItem[] in both runs");

                        multiItems!.Length.Should().Be(singleItems.Length,
                            $"[Output] {prop.Name} should have the same item count");

                        for (int i = 0; i < singleItems.Length; i++)
                        {
                            multiItems[i].ItemSpec.Should().Be(singleItems[i].ItemSpec,
                                $"[Output] {prop.Name}[{i}].ItemSpec should match");
                        }
                    }
                    else
                    {
                        multiValue.Should().BeEquivalentTo(singleValue,
                            $"[Output] {prop.Name} should be identical between single and multi-process modes");
                    }
                }
            }
            finally
            {
                try { Directory.Delete(projectDir, true); } catch { }
            }
        }

        [Fact]
        public void OutputPaths_AreNotAbsolutized_ByTaskEnvironment()
        {
            var projectDir = Path.Combine(Path.GetTempPath(), "rpa-noabs-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);

            // Use a separate packages folder to make assertions clear
            var packagesDir = Path.Combine(Path.GetTempPath(), "rpa-pkgs-" + Guid.NewGuid().ToString("N"));

            // TaskEnvironment points to a completely different directory
            var unrelatedDir = Path.Combine(Path.GetTempPath(), "rpa-unrelated-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(unrelatedDir);

            try
            {
                var objDir = Path.Combine(projectDir, "obj");
                Directory.CreateDirectory(objDir);

                // Create NuGet package folder structure that FallbackPackagePathResolver can find.
                // Required layout: {root}/{id}/{version}/{id}.{version}.nupkg.sha512
                var pkgDir = Path.Combine(packagesDir, "newtonsoft.json", "13.0.1");
                var libDir = Path.Combine(pkgDir, "lib", "net8.0");
                Directory.CreateDirectory(libDir);

                File.WriteAllText(
                    Path.Combine(pkgDir, "newtonsoft.json.13.0.1.nupkg.sha512"),
                    "abc123");
                File.WriteAllText(
                    Path.Combine(pkgDir, "newtonsoft.json.nuspec"),
                    "<package><metadata><id>Newtonsoft.Json</id><version>13.0.1</version></metadata></package>");
                File.WriteAllText(
                    Path.Combine(libDir, "Newtonsoft.Json.dll"),
                    string.Empty);

                var assetsJson = @"{
  ""version"": 3,
  ""targets"": {
    ""net8.0"": {
      ""Newtonsoft.Json/13.0.1"": {
        ""type"": ""package"",
        ""compile"": {
          ""lib/net8.0/Newtonsoft.Json.dll"": {}
        },
        ""runtime"": {
          ""lib/net8.0/Newtonsoft.Json.dll"": {}
        }
      }
    }
  },
  ""libraries"": {
    ""Newtonsoft.Json/13.0.1"": {
      ""sha512"": ""abc123"",
      ""type"": ""package"",
      ""path"": ""newtonsoft.json/13.0.1"",
      ""files"": [
        ""lib/net8.0/Newtonsoft.Json.dll"",
        ""newtonsoft.json.13.0.1.nupkg.sha512"",
        ""newtonsoft.json.nuspec""
      ]
    }
  },
  ""projectFileDependencyGroups"": {
    ""net8.0"": [""Newtonsoft.Json >= 13.0.1""]
  },
  ""packageFolders"": {
    """ + packagesDir.Replace("\\", "\\\\") + @""": {}
  },
  ""project"": {
    ""version"": ""1.0.0"",
    ""restore"": {
      ""projectUniqueName"": ""test"",
      ""projectName"": ""test"",
      ""projectPath"": """ + Path.Combine(projectDir, "test.csproj").Replace("\\", "\\\\") + @""",
      ""packagesPath"": """ + packagesDir.Replace("\\", "\\\\") + @""",
      ""outputPath"": """ + objDir.Replace("\\", "\\\\") + @""",
      ""projectStyle"": ""PackageReference"",
      ""frameworks"": {
        ""net8.0"": {
          ""targetAlias"": ""net8.0""
        }
      }
    },
    ""frameworks"": {
      ""net8.0"": {}
    }
  }
}";
                File.WriteAllText(Path.Combine(objDir, "project.assets.json"), assetsJson);

                // Set up the task with absolute paths for I/O, but TaskEnvironment pointing elsewhere
                var task = new ResolvePackageAssets
                {
                    BuildEngine = new MockBuildEngine(),
                    ProjectAssetsCacheFile = Path.Combine(objDir, "project.assets.cache"),
                    ProjectAssetsFile = Path.Combine(objDir, "project.assets.json"),
                    ProjectPath = Path.Combine(projectDir, "test.csproj"),
                    TargetFramework = "net8.0",
                    DotNetAppHostExecutableNameWithoutExtension = "apphost",
                    DefaultImplicitPackages = "Microsoft.NETCore.App",
                    DisablePackageAssetsCache = true,
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(unrelatedDir)
                };

                var result = task.Execute();
                result.Should().BeTrue("task should execute successfully with a valid package");

                // CompileTimeAssemblies and RuntimeAssemblies should be populated
                task.CompileTimeAssemblies.Should().NotBeEmpty(
                    "the lock file has a compile asset for Newtonsoft.Json");
                task.RuntimeAssemblies.Should().NotBeEmpty(
                    "the lock file has a runtime asset for Newtonsoft.Json");

                // Output paths must come from the NuGet package resolver (rooted at packagesDir),
                // NOT from TaskEnvironment.GetAbsolutePath (which would root at unrelatedDir).
                var expectedPrefix = Path.Combine(packagesDir, "newtonsoft.json", "13.0.1");
                foreach (var item in task.CompileTimeAssemblies)
                {
                    item.ItemSpec.Should().StartWith(expectedPrefix,
                        "CompileTimeAssemblies paths should resolve via NuGet package folder, not via TaskEnvironment");
                    item.ItemSpec.Should().NotStartWith(unrelatedDir,
                        "TaskEnvironment.ProjectDirectory must not leak into output paths");
                }

                foreach (var item in task.RuntimeAssemblies)
                {
                    item.ItemSpec.Should().StartWith(expectedPrefix,
                        "RuntimeAssemblies paths should resolve via NuGet package folder, not via TaskEnvironment");
                    item.ItemSpec.Should().NotStartWith(unrelatedDir,
                        "TaskEnvironment.ProjectDirectory must not leak into output paths");
                }
            }
            finally
            {
                try { Directory.Delete(projectDir, true); } catch { }
                try { Directory.Delete(packagesDir, true); } catch { }
                try { Directory.Delete(unrelatedDir, true); } catch { }
            }
        }

        private static ResolvePackageAssets CreateResolvePackageAssetsTask(string projectDir, string objDir)
        {
            return new ResolvePackageAssets
            {
                BuildEngine = new MockBuildEngine(),
                ProjectAssetsCacheFile = Path.Combine(objDir, "project.assets.cache"),
                ProjectAssetsFile = Path.Combine(objDir, "project.assets.json"),
                ProjectPath = Path.Combine(projectDir, "test.csproj"),
                TargetFramework = "net8.0",
                DotNetAppHostExecutableNameWithoutExtension = "apphost",
                DefaultImplicitPackages = "Microsoft.NETCore.App"
            };
        }
    }
}
