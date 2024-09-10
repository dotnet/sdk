﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tests
{
    public class ApplyDeltaTests(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
    {
        [Fact(Skip = "https://github.com/dotnet/sdk/issues/42850")]
        public async Task AddSourceFile()
        {
            Logger.WriteLine("AddSourceFile started");

            var testAsset = TestAssets.CopyTestAsset("WatchAppWithProjectDeps")
                .WithSource();

            var dependencyDir = Path.Combine(testAsset.Path, "Dependency");

            App.Start(testAsset, [], "AppWithDeps");

            await App.AssertWaitingForChanges();

            // add a new file:
            UpdateSourceFile(Path.Combine(dependencyDir, "AnotherLib.cs"), """
                public class AnotherLib
                {
                    public static void Print()
                        => System.Console.WriteLine("Changed!");
                }
                """);

            // update existing file:
            UpdateSourceFile(Path.Combine(dependencyDir, "Foo.cs"), """
                public class Lib
                {
                    public static void Print()
                        => AnotherLib.Print();
                }
                """);

            await App.AssertOutputLineStartsWith("Changed!");
        }

        [Fact(Skip = "https://github.com/dotnet/sdk/issues/42850")]
        public async Task ChangeFileInDependency()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchAppWithProjectDeps")
                .WithSource();

            var dependencyDir = Path.Combine(testAsset.Path, "Dependency");

            App.Start(testAsset, [], "AppWithDeps");

            await App.AssertWaitingForChanges();

            var newSrc = """
                public class Lib
                {
                    public static void Print()
                        => System.Console.WriteLine("Changed!");
                }
                """;

            UpdateSourceFile(Path.Combine(dependencyDir, "Foo.cs"), newSrc);

            await App.AssertOutputLineStartsWith("Changed!");
        }

        // Test is timing out on .NET Framework: https://github.com/dotnet/sdk/issues/41669
        [CoreMSBuildOnlyFact(Skip = "https://github.com/dotnet/sdk/issues/42850")]
        public async Task HandleTypeLoadFailure()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchAppTypeLoadFailure")
                .WithSource();

            App.Start(testAsset, [], "App");

            await App.AssertWaitingForChanges();

            var newSrc = """
                class DepSubType : Dep
                {
                    int F() => 2;
                }

                class Printer
                {
                    public static void Print()
                    {
                        Console.WriteLine("Changed!");
                    }
                }
                """;

            UpdateSourceFile(Path.Combine(testAsset.Path, "App", "Update.cs"), newSrc);

            await App.AssertOutputLineStartsWith("Updated types: Printer");
        }

        [Fact]
        public async Task BlazorWasm()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchBlazorWasm")
                .WithSource();

            App.Start(testAsset, [], testFlags: TestFlags.MockBrowser);

            await App.AssertOutputLineStartsWith(MessageDescriptor.ConfiguredToUseBrowserRefresh);
            await App.AssertOutputLineStartsWith(MessageDescriptor.ConfiguredToLaunchBrowser);
            await App.AssertOutputLineStartsWith("dotnet watch ⌚ Launching browser: http://localhost:5000/");
            await App.AssertWaitingForChanges();

            // TODO: enable once https://github.com/dotnet/razor/issues/10818 is fixed
            //var newSource = """
            //    @page "/"
            //    <h1>Updated</h1>
            //    """;

            //UpdateSourceFile(Path.Combine(testAsset.Path, "Pages", "Index.razor"), newSource);
            //await App.AssertOutputLineStartsWith(MessageDescriptor.HotReloadSucceeded);
        }
    }
}
