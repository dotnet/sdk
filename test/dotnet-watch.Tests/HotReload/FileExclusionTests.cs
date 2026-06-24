// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class FileExclusionTests : DotNetWatchTestBase
{
    public enum DirectoryKind
    {
        Ordinary,
        Hidden,
        Bin,
        Obj,
    }

    [TestMethod]
    [DataRow(true, true, DirectoryKind.Ordinary)]
    [DataRow(true, true, DirectoryKind.Hidden)]
    [DataRow(true, true, DirectoryKind.Bin)]
    [DataRow(true, true, DirectoryKind.Obj)]
    [DataRow(true, false, DirectoryKind.Ordinary)]
    [DataRow(true, false, DirectoryKind.Hidden)]
    [DataRow(true, false, DirectoryKind.Bin)]
    [DataRow(true, false, DirectoryKind.Obj)]
    [DataRow(false, true, DirectoryKind.Ordinary)]
    [DataRow(false, true, DirectoryKind.Hidden)]
    [DataRow(false, true, DirectoryKind.Bin)]
    [DataRow(false, true, DirectoryKind.Obj)]
    [DataRow(false, false, DirectoryKind.Ordinary)]
    [DataRow(false, false, DirectoryKind.Hidden)]
    [DataRow(false, false, DirectoryKind.Bin)]
    [DataRow(false, false, DirectoryKind.Obj)]
    public async Task IgnoredChange(bool isExisting, bool isIncluded, DirectoryKind directoryKind)
    {
        var testAsset = CopyTestAsset("WatchNoDepsApp", [isExisting, isIncluded, directoryKind]);

        var workingDirectory = testAsset.Path;
        string dir;

        switch (directoryKind)
        {
            case DirectoryKind.Bin:
                dir = Path.Combine(workingDirectory, "bin", "Debug", ToolsetInfo.CurrentTargetFramework);
                break;

            case DirectoryKind.Obj:
                dir = Path.Combine(workingDirectory, "obj", "Debug", ToolsetInfo.CurrentTargetFramework);
                break;

            case DirectoryKind.Hidden:
                dir = Path.Combine(workingDirectory, ".dir");
                break;

            default:
                dir = workingDirectory;
                break;
        }

        var extension = isIncluded ? ".cs" : ".txt";

        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, "File" + extension);

        if (isExisting)
        {
            File.WriteAllText(path, "class C { int F() => 1; }");

            if (isIncluded && directoryKind is DirectoryKind.Bin or DirectoryKind.Obj or DirectoryKind.Hidden)
            {
                var project = Path.Combine(workingDirectory, "WatchNoDepsApp.csproj");
                File.WriteAllText(project, File.ReadAllText(project).Replace(
                    "<!-- add item -->",
                    $"""
                    <Compile Include="{path}"/>
                    """));
            }
        }

        await using var w = CreateInProcWatcher(testAsset, ["--no-exit"], workingDirectory);

        var waitingForChanges = w.Observer.RegisterSemaphore(MessageDescriptor.WaitingForChanges);
        var changeHandled = w.Observer.RegisterSemaphore(MessageDescriptor.ManagedCodeChangesApplied);
        var ignoringChangeInHiddenDirectory = w.Observer.RegisterSemaphore(MessageDescriptor.IgnoringChangeInHiddenDirectory);
        var ignoringChangeInExcludedFile = w.Observer.RegisterSemaphore(MessageDescriptor.IgnoringChangeInExcludedFile);
        var fileAdditionTriggeredReEvaluation = w.Observer.RegisterSemaphore(MessageDescriptor.FileAdditionTriggeredReEvaluation);
        var reEvaluationCompleted = w.Observer.RegisterSemaphore(MessageDescriptor.ReEvaluationCompleted);
        var noHotReloadChangesToApply = w.Observer.RegisterSemaphore(MessageDescriptor.NoManagedCodeChangesToApply);

        w.Start();

        Log("Waiting for changes...");
        await waitingForChanges.WaitAsync(w.ShutdownSource.Token);
        
        UpdateSourceFile(path, "class C { int F() => 2; }");

        switch ((isExisting, isIncluded, directoryKind))
        {
            case (isExisting: true, isIncluded: true, directoryKind: _):
                Log("Waiting for changed handled ...");
                await changeHandled.WaitAsync(w.ShutdownSource.Token);
                break;

            case (isExisting: true, isIncluded: false, directoryKind: DirectoryKind.Ordinary):
                Log("Waiting for no hot reload changes to apply ...");
                await noHotReloadChangesToApply.WaitAsync(w.ShutdownSource.Token);
                break;

            case (isExisting: false, isIncluded: _, directoryKind: DirectoryKind.Ordinary):
                Log("Waiting for file addition re-evalutation ...");
                await fileAdditionTriggeredReEvaluation.WaitAsync(w.ShutdownSource.Token);
                Log("Waiting for re-evalutation to complete ...");
                await reEvaluationCompleted.WaitAsync(w.ShutdownSource.Token);
                break;

            case (isExisting: _, isIncluded: _, directoryKind: DirectoryKind.Hidden):
                Log("Waiting for ignored change in hidden dir ...");
                await ignoringChangeInHiddenDirectory.WaitAsync(w.ShutdownSource.Token);
                break;

            case (isExisting: _, isIncluded: _, directoryKind: DirectoryKind.Bin or DirectoryKind.Obj):
                Log("Waiting for ignored change in output dir ...");
                await ignoringChangeInExcludedFile.WaitAsync(w.ShutdownSource.Token);
                break;

            default:
                throw new InvalidOperationException();
        }
    }
}
