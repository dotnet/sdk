// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests
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

        /// <summary>
        /// Unchanged project doesn't build. Wait for source change and rebuild.
        /// </summary>
        [Fact]
        public async Task BaselineCompilationError()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchNoDepsApp")
                .WithSource();

            var programPath = Path.Combine(testAsset.Path, "Program.cs");
            File.WriteAllText(programPath,
                """
                Console.Write
                """);

            App.Start(testAsset, []);

            await App.AssertOutputLineStartsWith(MessageDescriptor.WaitingForFileChangeBeforeRestarting, failure: _ => false);

            UpdateSourceFile(programPath, """
                System.Console.WriteLine("<Updated>");
                """);

            await App.AssertOutputLineStartsWith("<Updated>");
        }

        [Fact]
        public async Task ChangeFileInFSharpProject()
        {
            var testAsset = TestAssets.CopyTestAsset("FSharpTestAppSimple")
                .WithSource();

            App.Start(testAsset, []);

            await App.AssertOutputLineStartsWith(MessageDescriptor.WaitingForFileChangeBeforeRestarting);

            UpdateSourceFile(Path.Combine(testAsset.Path, "Program.fs"), content => content.Replace("Hello World!", "<Updated>"));

            await App.AssertOutputLineStartsWith("<Updated>");
        }

        [Fact]
        public async Task ChangeFileInFSharpProjectWithLoop()
        {
            var testAsset = TestAssets.CopyTestAsset("FSharpTestAppSimple")
                .WithSource();

            var source = """
            module ConsoleApplication.Program

            open System
            open System.Threading

            [<EntryPoint>]
            let main argv =
                while true do
                    printfn "Waiting"
                    Thread.Sleep(200)
                0
            """;

            var sourcePath = Path.Combine(testAsset.Path, "Program.fs");

            File.WriteAllText(sourcePath, source);

            App.Start(testAsset, []);

            await App.AssertOutputLineStartsWith(MessageDescriptor.WaitingForChanges);

            UpdateSourceFile(sourcePath, content => content.Replace("Waiting", "<Updated>"));

            await App.AssertOutputLineStartsWith(MessageDescriptor.WaitingForChanges, failure: _ => false);
            await App.AssertOutputLineStartsWith("<Updated>");

            UpdateSourceFile(sourcePath, content => content.Replace("<Updated>", "<Updated2>"));

            await App.AssertOutputLineStartsWith(MessageDescriptor.WaitingForChanges, failure: _ => false);
            await App.AssertOutputLineStartsWith("<Updated2>");
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

            await App.WaitUntilOutputContains(
                "dotnet watch ⚠ [WatchHotReloadApp (net9.0)] Expected to find a static method 'ClearCache' or 'UpdateApplication' on type 'AppUpdateHandler, WatchHotReloadApp, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null' but neither exists.");
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

            await App.WaitUntilOutputContains("dotnet watch ⚠ [WatchHotReloadApp (net9.0)] Exception from 'System.Action`1[System.Type[]]': System.InvalidOperationException: Bug!");

            if (verbose)
            {
                App.AssertOutputContains("dotnet watch 🕵️ [WatchHotReloadApp (net9.0)] Deltas applied.");
            }
            else
            {
                // shouldn't see any agent messages:
                App.AssertOutputDoesNotContain("🕵️");
            }
        }

        [Fact]
        public async Task BlazorWasm()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchBlazorWasm")
                .WithSource();

            var port = TestOptions.GetTestPort();
            App.Start(testAsset, ["--urls", "http://localhost:" + port], testFlags: TestFlags.MockBrowser);

            await App.AssertWaitingForChanges();

            App.AssertOutputContains(MessageDescriptor.ConfiguredToUseBrowserRefresh);
            App.AssertOutputContains(MessageDescriptor.ConfiguredToLaunchBrowser);
            App.AssertOutputContains($"dotnet watch ⌚ Launching browser: http://localhost:{port}/");

            // shouldn't see any agent messages (agent is not loaded into blazor-devserver):
            AssertEx.DoesNotContain("🕵️", App.Process.Output);

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

            var port = TestOptions.GetTestPort();
            App.Start(testAsset, ["--urls", "http://localhost:" + port], testFlags: TestFlags.MockBrowser);

            await App.AssertOutputLineStartsWith("dotnet watch ⚠ msbuild: [Warning] Duplicate source file");
            await App.AssertWaitingForChanges();
        }

        [Fact]
        public async Task Razor_Component_ScopedCssAndStaticAssets()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchRazorWithDeps")
                .WithSource();

            var port = TestOptions.GetTestPort();
            App.Start(testAsset, ["--urls", "http://localhost:" + port], relativeProjectDirectory: "RazorApp", testFlags: TestFlags.MockBrowser);

            await App.AssertWaitingForChanges();

            App.AssertOutputContains(MessageDescriptor.ConfiguredToUseBrowserRefresh);
            App.AssertOutputContains(MessageDescriptor.ConfiguredToLaunchBrowser);
            App.AssertOutputContains($"dotnet watch ⌚ Launching browser: http://localhost:{port}/");
            App.Process.ClearOutput();

            var scopedCssPath = Path.Combine(testAsset.Path, "RazorClassLibrary", "Components", "Example.razor.css");

            var newCss = """
                .example {
                    color: blue;
                }
                """;

            UpdateSourceFile(scopedCssPath, newCss);
            await App.AssertOutputLineStartsWith("dotnet watch 🔥 Hot reload change handled");

            App.AssertOutputContains($"dotnet watch ⌚ Handling file change event for scoped css file {scopedCssPath}.");
            App.AssertOutputContains($"dotnet watch ⌚ [RazorClassLibrary (net9.0)] No refresh server.");
            App.AssertOutputContains($"dotnet watch ⌚ [RazorApp (net9.0)] Refreshing browser.");
            App.AssertOutputContains($"dotnet watch 🔥 Hot reload of scoped css succeeded.");
            App.AssertOutputContains($"dotnet watch ⌚ No C# changes to apply.");
            App.Process.ClearOutput();

            var cssPath = Path.Combine(testAsset.Path, "RazorApp", "wwwroot", "app.css");
            UpdateSourceFile(cssPath, content => content.Replace("background-color: white;", "background-color: red;"));

            await App.AssertOutputLineStartsWith("dotnet watch 🔥 Hot reload change handled");

            App.AssertOutputContains($"dotnet watch ⌚ Sending static file update request for asset 'app.css'.");
            App.AssertOutputContains($"dotnet watch ⌚ [RazorApp (net9.0)] Refreshing browser.");
            App.AssertOutputContains($"dotnet watch 🔥 Hot Reload of static files succeeded.");
            App.AssertOutputContains($"dotnet watch ⌚ No C# changes to apply.");
            App.Process.ClearOutput();
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

        [Fact]
        public async Task Aspire()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchAspire")
                .WithSource();

            var serviceSourcePath = Path.Combine(testAsset.Path, "WatchAspire.ApiService", "Program.cs");
            var serviceProjectPath = Path.Combine(testAsset.Path, "WatchAspire.ApiService", "WatchAspire.ApiService.csproj");
            var originalSource = File.ReadAllText(serviceSourcePath, Encoding.UTF8);

            App.Start(testAsset, ["-lp", "http"], relativeProjectDirectory: "WatchAspire.AppHost", testFlags: TestFlags.ReadKeyFromStdin);

            await App.AssertWaitingForChanges();

            // check that Aspire server output is logged via dotnet-watch reporter:
            await App.WaitUntilOutputContains("dotnet watch ⭐ Now listening on:");

            // wait until after DCP session started:
            await App.WaitUntilOutputContains("dotnet watch ⭐ Session started: #1");

            // valid code change:
            UpdateSourceFile(
                serviceSourcePath,
                originalSource.Replace("Enumerable.Range(1, 5)", "Enumerable.Range(1, 10)"));

            await App.AssertOutputLineStartsWith("dotnet watch 🔥 Hot reload change handled");

            App.AssertOutputContains("Using Aspire process launcher.");
            App.AssertOutputContains(MessageDescriptor.HotReloadSucceeded, "WatchAspire.AppHost (net9.0)");
            App.AssertOutputContains(MessageDescriptor.HotReloadSucceeded, "WatchAspire.ApiService (net9.0)");

            // Only one browser should be launched (dashboard). The child process shouldn't launch a browser.
            Assert.Equal(1, App.Process.Output.Count(line => line.StartsWith("dotnet watch ⌚ Launching browser: ")));
            App.Process.ClearOutput();

            // rude edit with build error:
            UpdateSourceFile(
                serviceSourcePath,
                originalSource.Replace("record WeatherForecast", "record WeatherForecast2"));

            await App.AssertOutputLineStartsWith("  ❔ Do you want to restart these projects? Yes (y) / No (n) / Always (a) / Never (v)");

            App.AssertOutputContains("dotnet watch ⌚ Unable to apply hot reload, restart is needed to apply the changes.");
            App.AssertOutputContains("error ENC0020: Renaming record 'WeatherForecast' requires restarting the application.");
            App.AssertOutputContains("dotnet watch ⌚ Affected projects:");
            App.AssertOutputContains("dotnet watch ⌚   WatchAspire.ApiService");
            App.Process.ClearOutput();

            App.SendKey('y');

            await App.AssertOutputLineStartsWith(MessageDescriptor.FixBuildError, failure: _ => false);

            // We don't have means to gracefully terminate process on Windows, see https://github.com/dotnet/runtime/issues/109432
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                App.AssertOutputContains("dotnet watch ❌ [WatchAspire.ApiService (net9.0)] Exited with error code -1");
            }
            else
            {
                // Unix process may return exit code = 128 + SIGTERM
                // dotnet watch ❌ [WatchAspire.ApiService (net9.0)] Exited with error code 143
                App.AssertOutputContains("[WatchAspire.ApiService (net9.0)] Exited");
            }

            App.AssertOutputContains($"dotnet watch ⌚ Building '{serviceProjectPath}' ...");
            App.AssertOutputContains("error CS0246: The type or namespace name 'WeatherForecast' could not be found");
            App.Process.ClearOutput();

            // fix build error:
            UpdateSourceFile(
                serviceSourcePath,
                originalSource.Replace("WeatherForecast", "WeatherForecast2"));

            await App.AssertOutputLineStartsWith("dotnet watch ⌚ [WatchAspire.ApiService (net9.0)] Capabilities");

            App.AssertOutputContains("dotnet watch ⌚ Build succeeded.");
            App.AssertOutputContains("dotnet watch 🔥 Project baselines updated.");
            App.AssertOutputContains($"dotnet watch ⭐ Starting project: {serviceProjectPath}");

            App.SendControlC();

            await App.AssertOutputLineStartsWith("dotnet watch 🛑 Shutdown requested. Press Ctrl+C again to force exit.");

            // We don't have means to gracefully terminate process on Windows, see https://github.com/dotnet/runtime/issues/109432
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await App.AssertOutputLineStartsWith("dotnet watch ❌ [WatchAspire.ApiService (net9.0)] Exited with error code -1");
                await App.AssertOutputLineStartsWith("dotnet watch ❌ [WatchAspire.AppHost (net9.0)] Exited with error code -1");
            }
            else
            {
                // Unix process may return exit code = 128 + SIGTERM
                // dotnet watch ❌ [WatchAspire.ApiService (net9.0)] Exited with error code 143
                await App.AssertOutputLine(line => line.Contains("[WatchAspire.ApiService (net9.0)] Exited"), failure: _ => false);
                await App.AssertOutputLine(line => line.Contains("[WatchAspire.AppHost (net9.0)] Exited"), failure: _ => false);
            }

            await App.AssertOutputLineStartsWith("dotnet watch ⭐ Waiting for server to shutdown ...");

            App.AssertOutputContains("dotnet watch ⭐ Stop session #1");
            App.AssertOutputContains("dotnet watch ⭐ [#1] Sending 'sessionTerminated'");
        }
    }
}
