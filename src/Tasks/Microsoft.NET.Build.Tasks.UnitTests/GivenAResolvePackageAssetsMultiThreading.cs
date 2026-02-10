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
    }
}
