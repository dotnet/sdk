// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests
{
    public class GlobbingAppTests : DotNetWatchTestBase
    {
        private const string AppName = "WatchGlobbingApp";

        public GlobbingAppTests(ITestOutputHelper logger)
            : base(logger)
        {
        }

        [ConditionalTheory(Skip = "https://github.com/dotnet/sdk/issues/42921")]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ChangeCompiledFile(bool usePollingWatcher)
        {
            var testAsset = _testAssetsManager.CopyTestAsset(AppName, identifier: usePollingWatcher.ToString())
               .WithSource();

            App.UsePollingWatcher = usePollingWatcher;
            App.Start(testAsset, ["--no-hot-reload"]);

            await AssertCompiledAppDefinedTypes(expected: 2);

            var fileToChange = Path.Combine(testAsset.Path, "include", "Foo.cs");
            var programCs = File.ReadAllText(fileToChange);
            File.WriteAllText(fileToChange, programCs);

            await App.AssertFileChanged();
            await App.AssertStarted();
            await AssertCompiledAppDefinedTypes(expected: 2);
        }

        [Fact(Skip = "https://github.com/dotnet/sdk/issues/42921")]
        public async Task DeleteCompiledFile()
        {
            var testAsset = _testAssetsManager.CopyTestAsset(AppName)
               .WithSource();

            App.Start(testAsset, ["--no-hot-reload"]);

            await AssertCompiledAppDefinedTypes(expected: 2);

            var fileToChange = Path.Combine(testAsset.Path, "include", "Foo.cs");
            File.Delete(fileToChange);

            await App.AssertStarted();
            await AssertCompiledAppDefinedTypes(expected: 1);
        }

        [Fact(Skip = "https://github.com/dotnet/sdk/issues/42921")]
        public async Task DeleteSourceFolder()
        {
            var testAsset = _testAssetsManager.CopyTestAsset(AppName)
               .WithSource();

            App.Start(testAsset, ["--no-hot-reload"]);

            await AssertCompiledAppDefinedTypes(expected: 2);

            var folderToDelete = Path.Combine(testAsset.Path, "include");
            Directory.Delete(folderToDelete, recursive: true);

            await App.AssertStarted();
            await AssertCompiledAppDefinedTypes(expected: 1);
        }

        [Fact(Skip = "https://github.com/dotnet/sdk/issues/42921")]
        public async Task RenameCompiledFile()
        {
            var testAsset = _testAssetsManager.CopyTestAsset(AppName)
               .WithSource();

            App.Start(testAsset, ["--no-hot-reload"]);

            await App.AssertStarted();

            var oldFile = Path.Combine(testAsset.Path, "include", "Foo.cs");
            var newFile = Path.Combine(testAsset.Path, "include", "Foo_new.cs");
            File.Move(oldFile, newFile);

            await App.AssertStarted();
        }

        [Fact(Skip = "https://github.com/dotnet/sdk/issues/42921")]
        public async Task ChangeExcludedFile()
        {
            var testAsset = _testAssetsManager.CopyTestAsset(AppName)
               .WithSource();

            App.Start(testAsset, ["--no-hot-reload"]);

            await App.AssertStarted();

            var changedFile = Path.Combine(testAsset.Path, "exclude", "Baz.cs");
            File.WriteAllText(changedFile, "");

            var fileChanged = App.AssertFileChanged();
            var finished = await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(5)), fileChanged);
            Assert.NotSame(fileChanged, finished);
        }

        [Fact]
        public async Task ListsFiles()
        {
            var testAsset = _testAssetsManager.CopyTestAsset(AppName)
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

        private async Task AssertCompiledAppDefinedTypes(int expected)
        {
            var prefix = "Defined types = ";

            var line = await App.AssertOutputLineStartsWith(prefix);
            Assert.Equal(expected, int.Parse(line));
        }
    }
}
