// Licensed to the .NET Foundation under one or more agreements.
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

        // Test is timing out on .NET Framework: https://github.com/dotnet/sdk/issues/41669
        [CoreMSBuildOnlyFact]
        public async Task HandleMissingAssemblyFailure()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchAppMissingAssemblyFailure")
                .WithSource();

            App.Start(testAsset, [], "App");

            await App.AssertWaitingForChanges();

            var newSrc = /* lang=c#-test */"""
                using System;

                public class DepType
                {
                    int F() => 1;
                }

                public class Printer
                {
                    public static void Print()
                        => Console.WriteLine("Updated!");
                }
                """;

            // Delete all files in testAsset.Path named Dep.dll
            foreach (var depDll in Directory.GetFiles(testAsset.Path, "Dep2.dll", SearchOption.AllDirectories))
            {
                File.Delete(depDll);
            }

            File.WriteAllText(Path.Combine(testAsset.Path, "App", "Update.cs"), newSrc);

            await App.AssertOutputLineStartsWith("Updated types: Printer");
        }

        [Theory]
        [InlineData(true, Skip = "https://github.com/dotnet/sdk/issues/43320")]
        [InlineData(false)]
        public async Task RenameSourceFile(bool useMove)
        {
            Logger.WriteLine("RenameSourceFile started");

            var testAsset = TestAssets.CopyTestAsset("WatchAppWithProjectDeps")
                .WithSource();

            var dependencyDir = Path.Combine(testAsset.Path, "Dependency");
            var oldFilePath = Path.Combine(dependencyDir, "Foo.cs");
            var newFilePath = Path.Combine(dependencyDir, "Renamed.cs");

            var source = """
                using System;
                using System.IO;
                using System.Runtime.CompilerServices;

                public class Lib
                {
                    public static void Print() => PrintFileName();

                    public static void PrintFileName([CallerFilePathAttribute] string filePath = null)
                    {
                        Console.WriteLine($"> {Path.GetFileName(filePath)}");
                    }
                }
                """;

            File.WriteAllText(oldFilePath, source);

            App.Start(testAsset, [], "AppWithDeps");

            await App.AssertWaitingForChanges();

            // rename the file:
            if (useMove)
            {
                File.Move(oldFilePath, newFilePath);
            }
            else
            {
                File.Delete(oldFilePath);
                File.WriteAllText(newFilePath, source);
            }

            Logger.WriteLine($"Renamed '{oldFilePath}' to '{newFilePath}'.");

            await App.AssertOutputLineStartsWith("> Renamed.cs");
        }

        [Theory]
        [InlineData(true, Skip = "https://github.com/dotnet/sdk/issues/43320")]
        [InlineData(false)]
        public async Task RenameDirectory(bool useMove)
        {
            Logger.WriteLine("RenameSourceFile started");

            var testAsset = TestAssets.CopyTestAsset("WatchAppWithProjectDeps")
                .WithSource();

            var dependencyDir = Path.Combine(testAsset.Path, "Dependency");
            var oldSubdir = Path.Combine(dependencyDir, "Subdir");
            var newSubdir = Path.Combine(dependencyDir, "NewSubdir");

            var source = """
                using System;
                using System.IO;
                using System.Runtime.CompilerServices;

                public class Lib
                {
                    public static void Print() => PrintDirectoryName();

                    public static void PrintDirectoryName([CallerFilePathAttribute] string filePath = null)
                    {
                        Console.WriteLine($"> {Path.GetFileName(Path.GetDirectoryName(filePath))}");
                    }
                }
                """;

            File.Delete(Path.Combine(dependencyDir, "Foo.cs"));
            Directory.CreateDirectory(oldSubdir);
            File.WriteAllText(Path.Combine(oldSubdir, "Foo.cs"), source);

            App.Start(testAsset, [], "AppWithDeps");

            await App.AssertWaitingForChanges();

            // rename the directory:
            if (useMove)
            {
                Directory.Move(oldSubdir, newSubdir);
            }
            else
            {
                Directory.Delete(oldSubdir, recursive: true);
                Directory.CreateDirectory(newSubdir);
                File.WriteAllText(Path.Combine(newSubdir, "Foo.cs"), source);
            }

            Logger.WriteLine($"Renamed '{oldSubdir}' to '{newSubdir}'.");

            await App.AssertOutputLineStartsWith("> NewSubdir");
        }
    }
}
