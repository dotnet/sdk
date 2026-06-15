// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Reflection;
using Microsoft.AspNetCore.Razor.Tasks;
using Microsoft.Build.Framework;
using Moq;
using TaskItem = Microsoft.Build.Utilities.TaskItem;

namespace Microsoft.NET.Sdk.Razor.Test
{
    public class FindAssembliesWithReferencesToMultiThreadingTest
    {
        [Fact]
        public void GetReferences_RelativePath_ResolvesAgainstTaskEnvironmentProjectDirectory()
        {
            // The point of the migration: relative paths must be absolutized against
            // TaskEnvironment.ProjectDirectory instead of the process CWD. The file is placed
            // in a temp directory distinct from CWD so pre-migration code would not find it.
            using var temp = new TempDirectory();
            const string fileName = "stub.dll";
            File.WriteAllBytes(Path.Combine(temp.Path, fileName), new byte[] { 0 });
            Directory.GetCurrentDirectory().Should().NotBe(temp.Path,
                "test must run with CWD distinct from the temp dir so the migration is actually exercised");

            var resolver = CreateExposingResolver(TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(temp.Path));

            resolver.CallGetReferences(fileName).Should().BeEmpty(
                "the file exists at TaskEnvironment.ProjectDirectory; PEReader's BadImageFormatException is swallowed and an empty list returned");
        }


        [Fact]
        public void Execute_PassesTaskEnvironmentToResolver_AndResolvesRelativeAssemblyItemSpecs()
        {
            // End-to-end wiring test: FindAssembliesWithReferencesTo must forward its
            // TaskEnvironment to the ReferenceResolver so relative ItemSpecs on Assemblies
            // are resolved against the project directory, not the process CWD.
            using var temp = new TempDirectory();
            const string fileName = "candidate.dll";
            File.WriteAllBytes(Path.Combine(temp.Path, fileName), new byte[] { 0 });

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
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(temp.Path),
                TargetAssemblyNames = new ITaskItem[] { new TaskItem("Microsoft.AspNetCore.Mvc") },
                Assemblies = new ITaskItem[]
                {
                    new TaskItem(fileName, new Dictionary<string, string>
                    {
                        ["FusionName"] = "Candidate, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                    }),
                },
            };

            task.Execute().Should().BeTrue();
            errors.Should().BeEmpty();
            warnings.Should().NotContain(w => w.Code == "RAZORSDK1007",
                "the file is present under TaskEnvironment.ProjectDirectory; no not-found warning should be raised");
        }

        private static TaskEnvironment CreateMultiThreadedEnvironment() =>
            TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(Path.GetTempPath());

        private static ExposingReferenceResolver CreateExposingResolver(TaskEnvironment env) =>
            new(Array.Empty<string>(), Array.Empty<AssemblyItem>(), env);

        private sealed class ExposingReferenceResolver : ReferenceResolver
        {
            public ExposingReferenceResolver(
                IReadOnlyList<string> targetAssemblies,
                IReadOnlyList<AssemblyItem> assemblyItems,
                TaskEnvironment taskEnvironment)
                : base(targetAssemblies, assemblyItems, taskEnvironment)
            {
            }

            public IReadOnlyList<AssemblyItem> CallGetReferences(string file) => GetReferences(file);
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
                try { Directory.Delete(Path, recursive: true); } catch { /* best effort */ }
            }
        }
    }
}
