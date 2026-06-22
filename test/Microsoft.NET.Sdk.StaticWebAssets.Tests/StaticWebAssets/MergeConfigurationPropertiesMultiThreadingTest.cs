// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

// Test parallelization is disabled assembly-wide via
// [assembly:CollectionBehavior(DisableTestParallelization = true)] in
// LegacyStaticWebAssetsV1IntegrationTest.cs, which already isolates the
// process-CWD mutation this test performs.
[TestClass]
public class MergeConfigurationPropertiesMultiThreadingTest
{
    [TestMethod]
    public void ResolvesProjectReferencePathRelativeToTaskEnvironmentProjectDirectory_NotProcessCurrentDirectory()
    {
        // Scope of this test: verify that the *ProjectReferences* side of the path-equality check
        // in FindMatchingProject is rooted against TaskEnvironment.ProjectDirectory rather than the
        // process current directory. The *CandidateConfigurations* side (configuration.GetMetadata("FullPath"))
        // is intentionally passed as an already-absolute path here — that mirrors what MSBuild's
        // well-known %(FullPath) modifier produces in MT mode (it resolves against the per-task
        // AsyncLocal working directory, not the process CWD) and is also what the equality check
        // requires in order to compare against the OS-canonical form on the other side.
        //
        // Layout (must place the two roots in different subtrees so a relative
        // "../reference/myRcl.csproj" produces *different* absolute paths
        // depending on which root it is resolved against):
        //   <testRoot>/project/                 <-- TaskEnvironment.ProjectDirectory
        //   <testRoot>/project/../reference/    <-- candidate's real location
        //   <testRoot>/decoy/spawn/             <-- process CWD (the "decoy")
        //   <testRoot>/decoy/reference/         <-- where the old CWD-based
        //                                            Path.GetFullPath would point
        var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(MergeConfigurationPropertiesMultiThreadingTest), Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(testRoot, "project");
        var spawnDir = Path.Combine(testRoot, "decoy", "spawn");
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(spawnDir);

        var relativeProjectReference = Path.Combine("..", "reference", "myRcl.csproj");

        // Candidate FullPath represents what MSBuild's well-known FullPath modifier
        // would produce in MT mode: the path resolved against the project directory.
        var candidateAbsolutePath = Path.GetFullPath(Path.Combine(projectDir, relativeProjectReference));

        // Sanity: the decoy CWD would produce a *different* absolute path for the
        // same relative input — that is what proves the equality check is using
        // the right root.
        var decoyAbsolutePath = Path.GetFullPath(Path.Combine(spawnDir, relativeProjectReference));
        candidateAbsolutePath.Should().NotBe(decoyAbsolutePath,
            "the test setup must place project and decoy in different parents so the migration is actually exercised");

        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(spawnDir);

            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new MergeConfigurationProperties
            {
                BuildEngine = buildEngine.Object,
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
                CandidateConfigurations = new[] { CreateCandidateProjectConfiguration(candidateAbsolutePath) },
                ProjectReferences = new[]
                {
                    CreateProjectReference(
                        project: Path.Combine("..", "myRcl", "myRcl.csproj"),
                        // Relative MSBuildSourceProjectFile — the task must resolve
                        // this against the TaskEnvironment.ProjectDirectory, not
                        // against Environment.CurrentDirectory.
                        msBuildSourceProjectFile: relativeProjectReference,
                        undefineProperties: "TargetFramework;RuntimeIdentifier")
                }
            };

            var result = task.Execute();

            result.Should().BeTrue("the task must find the project reference by absolutizing against the project directory, not the process CWD");
            errorMessages.Should().BeEmpty();
            task.ProjectConfigurations.Should().HaveCount(1);
            task.ProjectConfigurations[0].GetMetadata("Source").Should().Be("myRcl");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCurrentDirectory);
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    private static ITaskItem CreateCandidateProjectConfiguration(string project)
    {
        return new TaskItem(project, new Dictionary<string, string>
        {
            ["AdditionalPublishProperties"] = "",
            ["GetBuildAssetsTargets"] = "GetCurrentProjectBuildStaticWebAssetItems",
            ["GetPublishAssetsTargets"] = "ComputeReferencedStaticWebAssetsPublishManifest;GetCurrentProjectPublishStaticWebAssetItems",
            ["Version"] = "2",
            ["AdditionalBuildProperties"] = "",
            ["Source"] = "myRcl",
            ["AdditionalPublishPropertiesToRemove"] = "",
            ["AdditionalBuildPropertiesToRemove"] = "",
        });
    }

    private static ITaskItem CreateProjectReference(
        string project,
        string msBuildSourceProjectFile,
        string undefineProperties = "")
    {
        return new TaskItem(project, new Dictionary<string, string>
        {
            ["MSBuildSourceProjectFile"] = msBuildSourceProjectFile,
            ["UndefineProperties"] = undefineProperties,
            ["SetConfiguration"] = "",
            ["SetPlatform"] = "",
            ["SetTargetFramework"] = "",
            ["GlobalPropertiesToRemove"] = "",
        });
    }
}
