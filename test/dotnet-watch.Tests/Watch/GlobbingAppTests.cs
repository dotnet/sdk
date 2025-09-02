// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests
{
    public class GlobbingAppTests(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
    {
        private async Task ValidateOperation(Action<string> operation, int expectedTypesAfterOperation)
        {
            var testAsset = TestAssets.CopyTestAsset("WatchGlobbingApp")
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
            var fileChanged = App.AssertFileChanged();
            var finished = await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(5)), fileChanged);
            Assert.NotSame(fileChanged, finished);
        }

        [Fact]
        public async Task ListsFiles()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchGlobbingApp")
               .WithSource();

            App.DotnetWatchArgs.Clear();
            App.Start(testAsset, ["--list"]);
            var lines = await App.Process.GetAllOutputLinesAsync(CancellationToken.None);
            var files = lines.Where(l => !l.StartsWith("dotnet watch ⌚") && l.Trim() != "");

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
}
