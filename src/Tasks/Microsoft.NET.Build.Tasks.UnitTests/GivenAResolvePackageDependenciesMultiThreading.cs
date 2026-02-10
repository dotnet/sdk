// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using NuGet.ProjectModel;
using Xunit;
using static Microsoft.NET.Build.Tasks.UnitTests.LockFileSnippets;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAResolvePackageDependenciesMultiThreading
    {
        [Fact]
        public void ItImplementsIMultiThreadableTask()
        {
            var task = new ResolvePackageDependencies();
            task.Should().BeAssignableTo<IMultiThreadableTask>();
        }

        [Fact]
        public void ItHasMSBuildMultiThreadableTaskAttribute()
        {
            typeof(ResolvePackageDependencies).Should().BeDecoratedWith<MSBuildMultiThreadableTaskAttribute>();
        }

        [Fact]
        public void ItResolvesProjectReferencePathsViaTaskEnvironment()
        {
            // Create a temp directory to act as a fake project dir (different from CWD).
            // This ensures the test fails if the task uses Path.GetFullPath (CWD-relative)
            // instead of TaskEnvironment.GetAbsolutePath (ProjectDirectory-relative).
            var projectDir = Path.Combine(Path.GetTempPath(), "rpd-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            try
            {
                var projectPath = Path.Combine(projectDir, "myproject.csproj");
                File.WriteAllText(projectPath, "<Project/>");

                string classLibDefn = CreateProjectLibrary("ClassLib/1.0.0",
                    path: "../ClassLib/project.json",
                    msbuildProject: "../ClassLib/ClassLib.csproj");

                string targetLib = CreateTargetLibrary("ClassLib/1.0.0", "project");

                string lockFileContent = CreateLockFileSnippet(
                    targets: new string[] {
                        CreateTarget(".NETCoreApp,Version=v1.0", targetLib),
                    },
                    libraries: new string[] { classLibDefn },
                    projectFileDependencyGroups: new string[] {
                        ProjectGroup,
                        CreateProjectGroup(".NETCoreApp,Version=v1.0", "ClassLib >= 1.0.0"),
                    }
                );

                var lockFile = TestLockFiles.CreateLockFile(lockFileContent);
                var resolver = new MockPackageResolver();

                var task = new ResolvePackageDependencies(lockFile, resolver)
                {
                    ProjectAssetsFile = lockFile.Path,
                    ProjectPath = projectPath,
                    ProjectLanguage = null,
                    TargetFramework = null,
                };
                task.BuildEngine = new MockBuildEngine();

                // Set TaskEnvironment via reflection to avoid compile-time coupling.
                // This test fails if the task doesn't have a TaskEnvironment property.
                var teProp = task.GetType().GetProperty("TaskEnvironment");
                teProp.Should().NotBeNull("task must have a TaskEnvironment property (from IMultiThreadableTask)");
                teProp!.SetValue(task, TaskEnvironmentHelper.CreateForTest(projectDir));

                task.Execute().Should().BeTrue();

                // The resolved path for the project reference should be relative to
                // the project directory (via TaskEnvironment), not the process CWD.
                var classLibPkg = task.PackageDefinitions
                    .First(p => p.GetMetadata(MetadataKeys.Name) == "ClassLib");
                var resolvedPath = classLibPkg.GetMetadata(MetadataKeys.ResolvedPath);

                var expectedPath = Path.GetFullPath(Path.Combine(projectDir, "../ClassLib/ClassLib.csproj"));
                resolvedPath.Should().Be(expectedPath);
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        private static string CreateProjectGroup(string tfm, params string[] deps)
        {
            string depList = deps.Length > 0 ? string.Join(",", deps.Select(d => $"\"{d}\"")) : "";
            return $"\"{tfm}\": [{depList}]";
        }
    }
}
