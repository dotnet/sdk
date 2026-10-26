// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests;

public class ProjectUpdateInProcTests(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
{
    [Fact]
    public async Task ProjectAndSourceFileChange()
    {
        var testAsset = CopyTestAsset("WatchHotReloadApp");

        var workingDirectory = testAsset.Path;
        var projectPath = Path.Combine(testAsset.Path, "WatchHotReloadApp.csproj");
        var programPath = Path.Combine(testAsset.Path, "Program.cs");

        await using var w = CreateInProcWatcher(testAsset, [], workingDirectory);

        var fileChangesCompleted = w.CreateCompletionSource();
        w.Watcher.Test_FileChangesCompletedTask = fileChangesCompleted.Task;

        var waitingForChanges = w.Observer.RegisterSemaphore(MessageDescriptor.WaitingForChanges);

        var changeHandled = w.Observer.RegisterSemaphore(MessageDescriptor.ManagedCodeChangesApplied);

        var hasUpdatedOutput = w.CreateCompletionSource();
        w.Reporter.OnProcessOutput += line =>
        {
            if (line.Content.Contains("System.Xml.Linq.XDocument"))
            {
                hasUpdatedOutput.TrySetResult();
            }
        };

        w.Start();

        Log("Waiting for changes...");
        await waitingForChanges.WaitAsync(w.ShutdownSource.Token);

        // change the project and source files at the same time:

        UpdateSourceFile(programPath, src => src.Replace("""Console.WriteLine(".");""", """Console.WriteLine(typeof(XDocument));"""));
        UpdateSourceFile(projectPath, src => src.Replace("<!-- items placeholder -->", """<Using Include="System.Xml.Linq"/>"""));

        // done updating files:
        fileChangesCompleted.TrySetResult();

        Log("Waiting for change handled ...");
        await changeHandled.WaitAsync(w.ShutdownSource.Token);

        Log("Waiting for output 'System.Xml.Linq.XDocument'...");
        await hasUpdatedOutput.Task;
    }

    [Fact]
    public async Task ProjectAndSourceFileChange_AddProjectReference()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchAppWithProjectDeps")
            .WithSource()
            .WithProjectChanges(project =>
            {
                foreach (var r in project.Root!.Descendants().Where(e => e.Name.LocalName == "ProjectReference").ToArray())
                {
                    r.Remove();
                }
            });

        var appProjDir = Path.Combine(testAsset.Path, "AppWithDeps");
        var appProjFile = Path.Combine(appProjDir, "App.WithDeps.csproj");
        var appFile = Path.Combine(appProjDir, "Program.cs");

        UpdateSourceFile(appFile, code => code.Replace("Lib.Print();", "// Lib.Print();"));

        await using var w = CreateInProcWatcher(testAsset, [], appProjDir);

        var fileChangesCompleted = w.CreateCompletionSource();
        w.Watcher.Test_FileChangesCompletedTask = fileChangesCompleted.Task;

        var waitingForChanges = w.Observer.RegisterSemaphore(MessageDescriptor.WaitingForChanges);
        var projectChangeTriggeredReEvaluation = w.Observer.RegisterSemaphore(MessageDescriptor.ProjectChangeTriggeredReEvaluation);
        var projectsRebuilt = w.Observer.RegisterSemaphore(MessageDescriptor.ProjectsRebuilt);
        var projectDependenciesDeployed = w.Observer.RegisterSemaphore(MessageDescriptor.ProjectDependenciesDeployed);
        var managedCodeChangesApplied = w.Observer.RegisterSemaphore(MessageDescriptor.ManagedCodeChangesApplied);

        var hasUpdatedOutput = w.CreateCompletionSource();
        w.Reporter.OnProcessOutput += line =>
        {
            if (line.Content.Contains("<Lib>"))
            {
                hasUpdatedOutput.TrySetResult();
            }
        };

        w.Start();

        Log("Waiting for changes...");
        await waitingForChanges.WaitAsync(w.ShutdownSource.Token);

        // change the project and source files at the same time:

        UpdateSourceFile(appProjFile, src => src.Replace("""
            <ItemGroup />
            """, """
            <ItemGroup>
                <ProjectReference Include="..\Dependency\Dependency.csproj" />
            </ItemGroup>
            """));

        UpdateSourceFile(appFile, code => code.Replace("// Lib.Print();", "Lib.Print();"));

        // done updating files:
        fileChangesCompleted.TrySetResult();

        Log("Waiting for output '<Lib>'...");
        await hasUpdatedOutput.Task;

        AssertEx.ContainsSubstring("Resolving 'Dependency, Version=1.0.0.0'", w.Reporter.ProcessOutput);

        Assert.Equal(1, projectChangeTriggeredReEvaluation.CurrentCount);
        Assert.Equal(1, projectsRebuilt.CurrentCount);
        Assert.Equal(1, projectDependenciesDeployed.CurrentCount);
        Assert.Equal(1, managedCodeChangesApplied.CurrentCount);
    }

    [Fact]
    public async Task ProjectAndSourceFileChange_AddPackageReference()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp")
            .WithSource();

        var projFilePath = Path.Combine(testAsset.Path, "WatchHotReloadApp.csproj");
        var programFilePath = Path.Combine(testAsset.Path, "Program.cs");

        await using var w = CreateInProcWatcher(testAsset, []);

        var fileChangesCompleted = w.CreateCompletionSource();
        w.Watcher.Test_FileChangesCompletedTask = fileChangesCompleted.Task;

        var waitingForChanges = w.Observer.RegisterSemaphore(MessageDescriptor.WaitingForChanges);
        var projectChangeTriggeredReEvaluation = w.Observer.RegisterSemaphore(MessageDescriptor.ProjectChangeTriggeredReEvaluation);
        var projectsRebuilt = w.Observer.RegisterSemaphore(MessageDescriptor.ProjectsRebuilt);
        var projectDependenciesDeployed = w.Observer.RegisterSemaphore(MessageDescriptor.ProjectDependenciesDeployed);
        var managedCodeChangesApplied = w.Observer.RegisterSemaphore(MessageDescriptor.ManagedCodeChangesApplied);

        var hasUpdatedOutput = w.CreateCompletionSource();
        w.Reporter.OnProcessOutput += line =>
        {
            if (line.Content.Contains("Newtonsoft.Json.Linq.JToken"))
            {
                hasUpdatedOutput.TrySetResult();
            }
        };

        w.Start();

        Log("Waiting for changes...");
        await waitingForChanges.WaitAsync(w.ShutdownSource.Token);

        // change the project and source files at the same time:

        UpdateSourceFile(projFilePath, source => source.Replace("""
            <!-- items placeholder -->
            """, """
            <PackageReference Include="Newtonsoft.Json" Version="13.0.3"/>
            """));

        UpdateSourceFile(programFilePath, source => source.Replace("Console.WriteLine(\".\");", "Console.WriteLine(typeof(Newtonsoft.Json.Linq.JToken));"));

        // done updating files:
        fileChangesCompleted.TrySetResult();

        Log("Waiting for output 'Newtonsoft.Json.Linq.JToken'...");
        await hasUpdatedOutput.Task;

        AssertEx.ContainsSubstring("Resolving 'Newtonsoft.Json, Version=13.0.0.0'", w.Reporter.ProcessOutput);

        Assert.Equal(1, projectChangeTriggeredReEvaluation.CurrentCount);
        Assert.Equal(0, projectsRebuilt.CurrentCount);
        Assert.Equal(1, projectDependenciesDeployed.CurrentCount);
        Assert.Equal(1, managedCodeChangesApplied.CurrentCount);
    }
}
