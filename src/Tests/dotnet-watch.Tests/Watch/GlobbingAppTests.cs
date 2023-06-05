﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Watcher.Tools;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Watcher.Tests
{
    public class GlobbingAppTests : DotNetWatchTestBase
    {
        private const string AppName = "WatchGlobbingApp";

        public GlobbingAppTests(ITestOutputHelper logger)
            : base(logger)
        {
        }

        [ConditionalTheory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ChangeCompiledFile(bool usePollingWatcher)
        {
            var testAsset = TestAssets.CopyTestAsset(AppName, identifier: usePollingWatcher.ToString())
               .WithSource()
               .Path;

            App.UsePollingWatcher = usePollingWatcher;
            await App.StartWatcherAsync(testAsset);

            await AssertCompiledAppDefinedTypes(expected: 2);

            var fileToChange = Path.Combine(testAsset, "include", "Foo.cs");
            var programCs = File.ReadAllText(fileToChange);
            File.WriteAllText(fileToChange, programCs);

            await App.AssertFileChanged();
            await App.AssertRestarted();
            await AssertCompiledAppDefinedTypes(expected: 2);
        }

        [Fact]
        public async Task DeleteCompiledFile()
        {
            var testAsset = TestAssets.CopyTestAsset(AppName)
               .WithSource()
               .Path;

            await App.StartWatcherAsync(testAsset);

            await AssertCompiledAppDefinedTypes(expected: 2);

            var fileToChange = Path.Combine(testAsset, "include", "Foo.cs");
            File.Delete(fileToChange);

            await App.AssertRestarted();
            await AssertCompiledAppDefinedTypes(expected: 1);
        }

        [Fact]
        public async Task DeleteSourceFolder()
        {
            var testAsset = TestAssets.CopyTestAsset(AppName)
               .WithSource()
               .Path;

            await App.StartWatcherAsync(testAsset);

            await AssertCompiledAppDefinedTypes(expected: 2);

            var folderToDelete = Path.Combine(testAsset, "include");
            Directory.Delete(folderToDelete, recursive: true);

            await App.AssertRestarted();
            await AssertCompiledAppDefinedTypes(expected: 1);
        }

        [Fact]
        public async Task RenameCompiledFile()
        {
            var testAsset = TestAssets.CopyTestAsset(AppName)
               .WithSource()
               .Path;

            await App.StartWatcherAsync(testAsset);

            var oldFile = Path.Combine(testAsset, "include", "Foo.cs");
            var newFile = Path.Combine(testAsset, "include", "Foo_new.cs");
            File.Move(oldFile, newFile);

            await App.AssertRestarted();
        }

        [Fact]
        public async Task ChangeExcludedFile()
        {
            var testAsset = TestAssets.CopyTestAsset(AppName)
               .WithSource()
               .Path;

            await App.StartWatcherAsync(testAsset);

            var changedFile = Path.Combine(testAsset, "exclude", "Baz.cs");
            File.WriteAllText(changedFile, "");

            var fileChanged = App.AssertFileChanged();
            var finished = await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(5)), fileChanged);
            Assert.NotSame(fileChanged, finished);
        }

        [Fact]
        public async Task ListsFiles()
        {
            var testAsset = TestAssets.CopyTestAsset(AppName)
               .WithSource()
               .Path;

            App.Start(testAsset, new[] { "--list" });
            var lines = await App.Process.GetAllOutputLinesAsync(CancellationToken.None);
            var files = lines.Where(l => !l.StartsWith("watch :"));

            AssertEx.EqualFileList(
                testAsset,
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
