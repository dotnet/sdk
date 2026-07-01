// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.Razor.Tasks;
using Microsoft.Build.Framework;
using Moq;
using TaskItem = Microsoft.Build.Utilities.TaskItem;

namespace Microsoft.NET.Sdk.Razor.Test
{
    [TestClass]
    public class FindAssembliesWithReferencesToMultiThreadingTest
    {
        [TestMethod]
        public void Execute_ResolvesRelativeAssemblyItemSpec_AgainstTaskEnvironmentProjectDirectory()
        {
            using var temp = new TempDirectory();
            const string fileName = "candidate.dll";
            File.WriteAllBytes(Path.Combine(temp.Path, fileName), new byte[] { 0 });

            var (task, warnings, errors) = CreateTask(temp.Path, fileName);

            task.Execute().Should().BeTrue();
            errors.Should().BeEmpty();
            warnings.Should().NotContain(w => w.Code == "RAZORSDK1007",
                "the relative ItemSpec is absolutized against TaskEnvironment.ProjectDirectory where the file exists");
        }

        [TestMethod]
        public void Execute_DoesNotResolveRelativeAssemblyItemSpec_AgainstProcessCurrentDirectory()
        {
            using var projectDir = new TempDirectory();
            var fileName = "decoy-" + Guid.NewGuid().ToString("N") + ".dll";
            var cwdFile = Path.Combine(Directory.GetCurrentDirectory(), fileName);
            File.WriteAllBytes(cwdFile, new byte[] { 0 });
            try
            {
                var (task, warnings, errors) = CreateTask(projectDir.Path, fileName);

                task.Execute().Should().BeTrue();
                errors.Should().BeEmpty();
                warnings.Should().Contain(w => w.Code == "RAZORSDK1007",
                    "resolution is isolated to TaskEnvironment.ProjectDirectory, so a file present only in the process CWD must not be found");
            }
            finally
            {
                File.Delete(cwdFile);
            }
        }

        private static (FindAssembliesWithReferencesTo Task, List<BuildWarningEventArgs> Warnings, List<BuildErrorEventArgs> Errors) CreateTask(
            string projectDirectory,
            string assemblyItemSpec)
        {
            var warnings = new List<BuildWarningEventArgs>();
            var errors = new List<BuildErrorEventArgs>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogWarningEvent(It.IsAny<BuildWarningEventArgs>()))
                .Callback<BuildWarningEventArgs>(warnings.Add);
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(errors.Add);

            var task = new FindAssembliesWithReferencesTo
            {
                BuildEngine = buildEngine.Object,
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDirectory),
                TargetAssemblyNames = new ITaskItem[] { new TaskItem("Microsoft.AspNetCore.Mvc") },
                Assemblies = new ITaskItem[]
                {
                    new TaskItem(assemblyItemSpec, new Dictionary<string, string>
                    {
                        ["FusionName"] = "Candidate, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                    }),
                },
            };

            return (task, warnings, errors);
        }

        private sealed class TempDirectory : IDisposable
        {
            public TempDirectory()
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    nameof(FindAssembliesWithReferencesToMultiThreadingTest),
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public void Dispose()
            {
                try { Directory.Delete(Path, recursive: true); } catch { }
            }
        }
    }
}
