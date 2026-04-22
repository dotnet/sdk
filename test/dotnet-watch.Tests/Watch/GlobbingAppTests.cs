// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.Watch.UnitTests;

public class GlobbingAppTests(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
{
    private async Task ValidateOperation(
        Action<string> operation,
        int expectedTypesAfterOperation,
        [CallerMemberName] string callingMethod = "",
        [CallerFilePath] string? callerFilePath = null,
        string? identifier = "")
    {
        var testAsset = TestAssets.CopyTestAsset("WatchGlobbingApp", callingMethod, callerFilePath, identifier)
           .WithSource();

        App.Start(testAsset, ["--no-hot-reload"]);

        await App.WaitForOutputLineContaining(MessageDescriptor.WaitingForChanges);
        await App.WaitUntilOutputContains("Defined types = 2");
        App.Process.ClearOutput();

        operation(testAsset.Path);

        await App.WaitUntilOutputContains($"Defined types = {expectedTypesAfterOperation}");
    }

    [Fact]
    public async Task ChangeCompiledFile()
    {
        await ValidateOperation(
            projectDir =>
            {
                UpdateSourceFile(Path.Combine(projectDir, "include", "Foo.cs"), src => src);
            },
            expectedTypesAfterOperation: 2);
    }

    [Fact]
    public async Task DeleteCompiledFile()
    {
        await ValidateOperation(
            projectDir =>
            {
                File.Delete(Path.Combine(projectDir, "include", "Foo.cs"));
            },
            expectedTypesAfterOperation: 1);
    }

    [Fact]
    public async Task DeleteSourceFolder()
    {
        await ValidateOperation(
            projectDir =>
            {
                Directory.Delete(Path.Combine(projectDir, "include"), recursive: true);
            },
            expectedTypesAfterOperation: 1);
    }

    [Fact]
    public async Task RenameCompiledFile()
    {
        await ValidateOperation(
            projectDir =>
            {
                var oldFile = Path.Combine(projectDir, "include", "Foo.cs");
                var newFile = Path.Combine(projectDir, "include", "Foo_new.cs");
                File.Move(oldFile, newFile);
            },
            expectedTypesAfterOperation: 2);
    }

    [Fact]
    public async Task ChangeExcludedFile()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchGlobbingApp")
           .WithSource();

        App.Start(testAsset, ["--no-hot-reload"]);

        await App.WaitForOutputLineContaining(MessageDescriptor.WaitingForChanges);
        await App.WaitUntilOutputContains("Defined types = 2");
        App.Process.ClearOutput();

        var changedFile = Path.Combine(testAsset.Path, "exclude", "Baz.cs");
        File.WriteAllText(changedFile, "");

        // no file change within timeout:
        var fileChanged = App.AssertOutputLineStartsWith("dotnet watch ⌚ File changed:");
        var finished = await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken), fileChanged);
        Assert.NotSame(fileChanged, finished);
    }

    [Fact]
    public async Task ListsFiles()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchGlobbingApp")
           .WithSource();

        App.SuppressVerboseLogging();
        App.Start(testAsset, ["--list"]);
        await App.Process.WaitUntilOutputCompleted();
        var files = App.Process.Output.Where(l => !l.StartsWith("dotnet watch ⌚") && l.Trim() != "");

        AssertEx.EqualFileList(
            testAsset.Path,
            new[]
            {
                "Program.cs",
                "include/Foo.cs",
                "WatchGlobbingApp.csproj",
            },
            files);
    }
}
