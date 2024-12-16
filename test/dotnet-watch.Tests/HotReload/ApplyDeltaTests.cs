// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.DotNet.Watcher.Tools;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tests
{
    public class ApplyDeltaTests(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
    {
        [Fact]
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

        [Fact]
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
        [CoreMSBuildOnlyFact]
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
        public async Task MetadataUpdateHandler_NoActions()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp")
                .WithSource();

            var sourcePath = Path.Combine(testAsset.Path, "Program.cs");

            var source = File.ReadAllText(sourcePath, Encoding.UTF8)
                .Replace("// <metadata update handler placeholder>", """
                [assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(AppUpdateHandler))]
                """)
                + """
                class AppUpdateHandler
                {
                }
                """;

            File.WriteAllText(sourcePath, source, Encoding.UTF8);

            App.Start(testAsset, []);

            await App.AssertWaitingForChanges();

            UpdateSourceFile(sourcePath, source.Replace("Console.WriteLine(\".\");", "Console.WriteLine(\"Updated\");"));

            await App.AssertOutputLineStartsWith("Updated");

            AssertEx.ContainsRegex(
                @"dotnet watch ⚠ \[WatchHotReloadApp \(net\d+\.\d+\)\] Expected to find a static method 'ClearCache' or 'UpdateApplication' on type 'AppUpdateHandler, WatchHotReloadApp, Version=1\.0\.0\.0, Culture=neutral, PublicKeyToken=null' but neither exists.",
                App.Process.Output);
        }

        [Theory]
        [CombinatorialData]
        public async Task MetadataUpdateHandler_Exception(bool verbose)
        {
            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp", identifier: verbose.ToString())
                .WithSource();

            var sourcePath = Path.Combine(testAsset.Path, "Program.cs");

            var source = File.ReadAllText(sourcePath, Encoding.UTF8)
                .Replace("// <metadata update handler placeholder>", """
                [assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(AppUpdateHandler))]
                """)
                + """
                class AppUpdateHandler
                {
                    public static void ClearCache(Type[] types) => throw new System.InvalidOperationException("Bug!");
                }
                """;

            File.WriteAllText(sourcePath, source, Encoding.UTF8);

            if (!verbose)
            {
                // remove default --verbose arg
                App.DotnetWatchArgs.Clear();
            }

            App.Start(testAsset, [], testFlags: TestFlags.ElevateWaitingForChangesMessageSeverity);

            await App.AssertWaitingForChanges();

            UpdateSourceFile(sourcePath, source.Replace("Console.WriteLine(\".\");", "Console.WriteLine(\"Updated\");"));

            await App.AssertOutputLineStartsWith("Updated");

            AssertEx.ContainsRegex(
                @"dotnet watch ⚠ \[WatchHotReloadApp \(net\d+\.\d+\)\] Exception from 'System.Action`1\[System.Type\[\]\]': System.InvalidOperationException: Bug!",
                App.Process.Output);

            if (verbose)
            {
                AssertEx.ContainsRegex(@"dotnet watch 🕵️ \[WatchHotReloadApp \(net\d+\.\d+\)\] Deltas applied.", App.Process.Output);
            }
            else
            {
                // shouldn't see any agent messages:
                AssertEx.DoesNotContain("🕵️", App.Process.Output);
            }
        }

        [Fact(Skip = "https://github.com/dotnet/sdk/issues/42920")]
        public async Task BlazorWasm()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchBlazorWasm")
                .WithSource();

            App.Start(testAsset, [], testFlags: TestFlags.MockBrowser);

            await App.AssertWaitingForChanges();

            App.AssertOutputContains(MessageDescriptor.ConfiguredToUseBrowserRefresh);
            App.AssertOutputContains(MessageDescriptor.ConfiguredToLaunchBrowser);
            App.AssertOutputContains("dotnet watch ⌚ Launching browser: http://localhost:5000/");

            var newSource = """
                @page "/"
                <h1>Updated</h1>
                """;

            UpdateSourceFile(Path.Combine(testAsset.Path, "Pages", "Index.razor"), newSource);
            await App.AssertOutputLineStartsWith(MessageDescriptor.HotReloadSucceeded, "blazorwasm (net9.0)");
        }

        [Fact]
        public async Task BlazorWasm_MSBuildWarning()
        {
            var testAsset = TestAssets
                .CopyTestAsset("WatchBlazorWasm")
                .WithSource()
                .WithProjectChanges(proj =>
                {
                    proj.Root.Descendants()
                        .Single(e => e.Name.LocalName == "ItemGroup")
                        .Add(XElement.Parse("""
                            <AdditionalFiles Include="Pages\Index.razor" />
                            """));
                });

            App.Start(testAsset, [], testFlags: TestFlags.MockBrowser);

            await App.AssertOutputLineStartsWith("dotnet watch ⚠ msbuild: [Warning] Duplicate source file");
            await App.AssertWaitingForChanges();
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

        [Fact(Skip = "https://github.com/dotnet/sdk/issues/42850")]
        public async Task Aspire()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchAspire")
                .WithSource();

            var workloadInstallCommandSpec = new DotnetCommand(Logger, ["workload", "install", "aspire", "--include-previews"])
            {
                WorkingDirectory = testAsset.Path,
            };

            var result = workloadInstallCommandSpec.Execute();
            Assert.Equal(0, result.ExitCode);

            var serviceSourcePath = Path.Combine(testAsset.Path, "WatchAspire.ApiService", "Program.cs");
            App.Start(testAsset, ["-lp", "http"], relativeProjectDirectory: "WatchAspire.AppHost");

            await App.AssertWaitingForChanges();

            // check that Aspire server output is logged via dotnet-watch reporter:
            await App.WaitUntilOutputContains("dotnet watch ⭐ Now listening on:");

            // wait until after DCP session started:
            await App.WaitUntilOutputContains("dotnet watch ⭐ Session started: #1");

            var newSource = File.ReadAllText(serviceSourcePath, Encoding.UTF8);
            newSource = newSource.Replace("Enumerable.Range(1, 5)", "Enumerable.Range(1, 10)");
            UpdateSourceFile(serviceSourcePath, newSource);

            await App.AssertOutputLineStartsWith("dotnet watch 🔥 Hot reload change handled");

            App.AssertOutputContains("Using Aspire process launcher.");
            App.AssertOutputContains(MessageDescriptor.HotReloadSucceeded, "WatchAspire.AppHost (net10.0)");
            App.AssertOutputContains(MessageDescriptor.HotReloadSucceeded, "WatchAspire.ApiService (net10.0)");

            // Only one browser should be launched (dashboard). The child process shouldn't launch a browser.
            Assert.Equal(1, App.Process.Output.Count(line => line.StartsWith("dotnet watch ⌚ Launching browser: ")));
        }
    }
}
