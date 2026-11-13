// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class CompilationHandlerTests : DotNetWatchTestBase
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MultiTargetedProject_PreviouslyBuiltTarget(bool buildOtherTarget)
    {
        var project = new TestProject("Counter")
        {
            IsExe = true,
            TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework};netstandard2.0",
            AdditionalProperties =
            {
                ["ImplicitUsings"] = "enable",
                ["LangVersion"] = "preview",
            },
            SourceFiles =
            {
                ["Program.cs"] = """
                    #if NET
                    while (true)
                    {
                        Counter.Print(EventArgs.Empty);
                        await Task.Delay(100);
                    }
                    #endif

                    static class Counter
                    {
                        public static void Print(EventArgs e) => Console.WriteLine("Before");
                    }
                    """,
            },
        };

        var testAsset = TestAssets.CreateTestProject(project, identifier: buildOtherTarget.ToString())
            .WithProjectChanges(document => document.Root!.Add(
                new XElement("PropertyGroup",
                    new XAttribute("Condition", "'$(TargetFramework)' == 'netstandard2.0'"),
                    new XElement("OutputType", "Library"))));
        var sourcePath = Path.Combine(testAsset.Path, project.Name!, "Program.cs");

        if (buildOtherTarget)
        {
            // This target has a current DLL/PDB but won't receive watch's design-time build.
            new DotnetBuildCommand(testAsset).Execute("-f", "netstandard2.0").Should().Pass();
        }

        App.Start(testAsset, ["-f", ToolsetInfo.CurrentTargetFramework], project.Name!);
        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

        UpdateSourceFile(sourcePath, source => source.Replace("Before", "After"));

        await App.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);
        await App.WaitUntilOutputContains("After");
    }

    [TestMethod]
    public async Task ReferenceOutputAssembly_False()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchAppMultiProc")
            .WithSource();

        UpdateSourceFile(Path.Combine(testAsset.Path, "Lib2", "Lib2.csproj"),
            source => source.Replace(ToolsetInfo.CurrentTargetFramework, "netstandard2.0"));

        var hostDir = Path.Combine(testAsset.Path, "Host");
        var hostProject = Path.Combine(hostDir, "Host.csproj");
        var hostProjectRepr = new ProjectRepresentation(hostProject, entryPointFilePath: null);

        var cmdOptions = TestOptions.GetCommandLineOptions(["--project", hostProject]);
        var environmentOptions = TestOptions.GetEnvironmentOptions(Environment.CurrentDirectory);

        var factory = new ProjectGraphFactory(
            [hostProjectRepr],
            EvaluationResult.GetGlobalBuildProperties([]),
            NullLogger.Instance,
            cmdOptions.GlobalOptions,
            environmentOptions);
        var projectGraph = factory.TryLoadProjectGraph(projectGraphRequired: false, virtualProjectTargetFramework: null, CancellationToken.None);
        Assert.IsNotNull(projectGraph);

        var evaluation = await EvaluationResult.TryCreateAsync(
            projectGraph, NullLogger.Instance, cmdOptions.GlobalOptions, environmentOptions,
            mainProjectTargetFramework: null, restore: true, CancellationToken.None);
        Assert.IsNotNull(evaluation);

        var handler = new RunningProjectsManager(new ProcessRunner(processCleanupTimeout: TimeSpan.Zero), NullLogger.Instance);
        using var workspace = new ManagedCodeWorkspace(NullLogger.Instance, handler);

        var solution = await workspace.UpdateProjectGraphAsync(evaluation, CancellationToken.None);

        // all projects are present
        AssertEx.SequenceEqual(["Host", "Lib2", "Lib", "A", "B"], solution.Projects.Select(p => p.Name));

        // Host does not have project reference to A, B:
        AssertEx.SequenceEqual(["Lib2"],
            solution.Projects.Single(p => p.Name == "Host").ProjectReferences
                .Select(r => solution.GetProject(r.ProjectId)!.Name));
    }
}
