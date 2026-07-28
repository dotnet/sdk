// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.AspNetCore.Razor.Tasks;

// Test parallelization is disabled assembly-wide: the MSTest.Sdk project sets
// MSTestParallelizeScope=None, which emits [assembly: DoNotParallelize] and runs
// tests sequentially, isolating the process-CWD mutation this test performs.
[TestClass]
public class ConcatenateCssFilesMultiThreadingTest
{
    [TestMethod]
    public void ReadsInputsAndWritesBundleRelativeToTaskEnvironmentProjectDirectory_NotProcessCurrentDirectory()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(ConcatenateCssFilesMultiThreadingTest), Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(testRoot, "project");
        var spawnDir = Path.Combine(testRoot, "spawn");
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(spawnDir);

        // The scoped css input lives under the project directory and is referenced with a relative
        // ItemSpec. It must be resolved against TaskEnvironment.ProjectDirectory, not the process CWD.
        const string inputContents = ".counter { color: red; }";
        File.WriteAllText(Path.Combine(projectDir, "Counter.razor.rz.scp.css"), inputContents);

        var relativeOutputFile = Path.Combine("obj", "scoped.styles.css");

        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(spawnDir);

            var task = new ConcatenateCssFiles
            {
                BuildEngine = new Mock<IBuildEngine>().Object,
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
                ScopedCssFiles =
                [
                    CreateStaticAsset("Counter.razor.rz.scp.css", "_content/Test/", "Counter.razor.rz.scp.css")
                ],
                ProjectBundles = [],
                ScopedCssBundleBasePath = "/",
                OutputFile = relativeOutputFile
            };

            task.Execute().Should().BeTrue("the task must run to completion when TaskEnvironment.ProjectDirectory differs from the process CWD");

            var expectedOutput = Path.Combine(projectDir, relativeOutputFile);
            File.Exists(expectedOutput).Should().BeTrue("the bundle should be written under the project dir, not the process CWD");

            var incorrectOutput = Path.Combine(spawnDir, relativeOutputFile);
            File.Exists(incorrectOutput).Should().BeFalse();

            // The input file content must have been read from the project directory.
            File.ReadAllText(expectedOutput).Should().Contain(inputContents);
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

    private static ITaskItem CreateStaticAsset(string identity, string basePath, string relativePath) =>
        new TaskItem(
            identity,
            new Dictionary<string, string>
            {
                ["BasePath"] = basePath,
                ["RelativePath"] = relativePath,
            });
}
