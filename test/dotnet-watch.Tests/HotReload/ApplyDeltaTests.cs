// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Watch.UnitTests
{
    public class ApplyDeltaTests(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
    {
        [Fact]
        public async Task AddSourceFile()
        {
            Log("AddSourceFile started");

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

            await App.AssertOutputLineStartsWith(MessageDescriptor.ReEvaluationCompleted);

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

        [Fact]
        public async Task ProjectChange_UpdateDirectoryBuildPropsThenUpdateSource()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchAppWithProjectDeps")
                .WithSource();

            var dependencyDir = Path.Combine(testAsset.Path, "Dependency");

            App.Start(testAsset, [], "AppWithDeps");

            await App.AssertWaitingForChanges();

            UpdateSourceFile(
                Path.Combine(testAsset.Path, "Directory.Build.props"),
                src => src.Replace("<AllowUnsafeBlocks>false</AllowUnsafeBlocks>", "<AllowUnsafeBlocks>true</AllowUnsafeBlocks>"));

            await App.AssertOutputLineStartsWith(MessageDescriptor.NoCSharpChangesToApply);
            App.AssertOutputContains(MessageDescriptor.ProjectChangeTriggeredReEvaluation);
            App.Process.ClearOutput();

            var newSrc = """
                public class Lib
                {
                    public static unsafe void Print()
                    {
                        char c = '!';
                        char* pc = &c;
                        System.Console.WriteLine($"Changed{*pc}");
                    }
                }
                """;

            UpdateSourceFile(Path.Combine(dependencyDir, "Foo.cs"), newSrc);

            await App.AssertOutputLineStartsWith("Changed!");
            await App.WaitUntilOutputContains($"dotnet watch 🔥 [App.WithDeps ({ToolsetInfo.CurrentTargetFramework})] Hot reload succeeded.");
        }

        [Theory]
        [CombinatorialData]
        public async Task ProjectChange_Update(bool isDirectoryProps)
        {
            var testAsset = TestAssets.CopyTestAsset("WatchAppWithProjectDeps", identifier: isDirectoryProps.ToString())
                .WithSource();

            var symbolName = isDirectoryProps ? "BUILD_CONST_IN_PROPS" : "BUILD_CONST_IN_CSPROJ";

            var dependencyDir = Path.Combine(testAsset.Path, "Dependency");
            var libSourcePath = Path.Combine(dependencyDir, "Foo.cs");
            var buildFilePath = isDirectoryProps ? Path.Combine(testAsset.Path, "Directory.Build.props") : Path.Combine(dependencyDir, "Dependency.csproj");

            File.WriteAllText(libSourcePath, $$"""
                public class Lib
                {
                    public static void Print()
                    {
                #if {{symbolName}}
                        System.Console.WriteLine("{{symbolName}} set");
                #else
                        System.Console.WriteLine("{{symbolName}} not set");
                #endif
                    }
                }
                """);

            App.Start(testAsset, [], "AppWithDeps");

            await App.AssertWaitingForChanges();
            await App.WaitUntilOutputContains($"{symbolName} set");
            App.Process.ClearOutput();

            UpdateSourceFile(buildFilePath, src => src.Replace(symbolName, ""));

            // TODO: https://github.com/dotnet/roslyn/issues/78921

            // Roslyn should detect change in build constant and apply it or flag it as rude edit.
            //await App.WaitUntilOutputContains($"dotnet watch 🔥 [App.WithDeps ({ToolsetInfo.CurrentTargetFramework})] Hot reload succeeded.");
            //await App.WaitUntilOutputContains("BUILD_CONST not set");

            await App.AssertOutputLineStartsWith(MessageDescriptor.NoCSharpChangesToApply);
            App.AssertOutputContains(MessageDescriptor.ProjectChangeTriggeredReEvaluation);
        }

        [Fact(Skip = "https://github.com/dotnet/msbuild/issues/12001")]
        public async Task ProjectChange_DirectoryBuildProps_Add()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchAppWithProjectDeps")
                .WithSource();

            var dependencyDir = Path.Combine(testAsset.Path, "Dependency");
            var libSourcePath = Path.Combine(dependencyDir, "Foo.cs");
            var directoryBuildProps = Path.Combine(testAsset.Path, "Directory.Build.props");

            // delete the file before we start the app, it will be added later:
            File.Delete(directoryBuildProps);

            File.WriteAllText(libSourcePath, """
                public class Lib
                {
                    public static void Print()
                    {
                #if BUILD_CONST_IN_PROPS
                        System.Console.WriteLine("BUILD_CONST_IN_PROPS set");
                #else                
                        System.Console.WriteLine("BUILD_CONST_IN_PROPS not set");
                #endif
                    }
                }
                """);

            App.Start(testAsset, [], "AppWithDeps");

            await App.AssertWaitingForChanges();
            await App.WaitUntilOutputContains("BUILD_CONST_IN_PROPS set");
            App.Process.ClearOutput();

            UpdateSourceFile(
                directoryBuildProps,
                src => src.Replace("BUILD_CONST_IN_PROPS", ""));

            await App.WaitUntilOutputContains($"dotnet watch 🔥 [App.WithDeps ({ToolsetInfo.CurrentTargetFramework})] Hot reload succeeded.");
            await App.WaitUntilOutputContains("BUILD_CONST not set");

            App.AssertOutputContains(MessageDescriptor.ProjectChangeTriggeredReEvaluation);
        }

        [Fact(Skip = "https://github.com/dotnet/sdk/issues/49545")]
        public async Task ProjectChange_DirectoryBuildProps_Delete()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchAppWithProjectDeps")
                .WithSource();

            var dependencyDir = Path.Combine(testAsset.Path, "Dependency");
            var libSourcePath = Path.Combine(dependencyDir, "Foo.cs");
            var directoryBuildProps = Path.Combine(testAsset.Path, "Directory.Build.props");

            File.WriteAllText(libSourcePath, """
                public class Lib
                {
                    public static void Print()
                    {
                #if BUILD_CONST_IN_PROPS
                        System.Console.WriteLine("BUILD_CONST_IN_PROPS set");
                #else
                        System.Console.WriteLine("BUILD_CONST_IN_PROPS not set");
                #endif
                    }
                }
                """);

            App.Start(testAsset, [], "AppWithDeps");

            await App.AssertWaitingForChanges();
            await App.WaitUntilOutputContains("BUILD_CONST_IN_PROPS set");

            Log($"Deleting {directoryBuildProps}");
            File.Delete(directoryBuildProps);

            await App.AssertOutputLineStartsWith(MessageDescriptor.NoCSharpChangesToApply);
            App.AssertOutputContains(MessageDescriptor.ProjectChangeTriggeredReEvaluation);
            App.Process.ClearOutput();

            await App.AssertOutputLineStartsWith("BUILD_CONST_IN_PROPS not set");
        }

        [Fact]
        public async Task DefaultItemExcludes_DefaultItemsEnabled()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    project.Root.Descendants()
                        .First(e => e.Name.LocalName == "PropertyGroup")
                        .Add(XElement.Parse("""
                            <DefaultItemExcludes>$(DefaultItemExcludes);AppData/**/*.*</DefaultItemExcludes>
                            """));
                });

            var appDataDir = Path.Combine(testAsset.Path, "AppData", "dir");
            var appDataFilePath = Path.Combine(appDataDir, "ShouldBeIgnored.cs");

            Directory.CreateDirectory(appDataDir);

            App.Start(testAsset, []);

            await App.AssertWaitingForChanges();
            App.AssertOutputContains(new Regex(@"dotnet watch ⌚ Exclusion glob: 'AppData/[*][*]/[*][.][*];bin[/\\]+Debug[/\\]+[*][*];obj[/\\]+Debug[/\\]+[*][*];bin[/\\]+[*][*];obj[/\\]+[*][*]"));
            App.Process.ClearOutput();

            UpdateSourceFile(appDataFilePath, """
            class X;
            """);

            await App.WaitUntilOutputContains($"dotnet watch ⌚ Ignoring change in excluded file '{appDataFilePath}': Add. Path matches DefaultItemExcludes glob 'AppData/**/*.*' set in '{testAsset.Path}'.");
        }

        [Fact]
        public async Task DefaultItemExcludes_DefaultItemsDisabled()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    project.Root.Descendants()
                        .First(e => e.Name.LocalName == "PropertyGroup")
                        .Add(XElement.Parse("""
                            <EnableDefaultItems>false</EnableDefaultItems>
                            """));

                    project.Root.Descendants()
                        .First(e => e.Name.LocalName == "ItemGroup")
                        .Add(XElement.Parse("""
                            <Compile Include="Program.cs" />
                            """));
                });

            var binDir = Path.Combine(testAsset.Path, "bin", "Debug", ToolsetInfo.CurrentTargetFramework);
            var binDirFilePath = Path.Combine(binDir, "ShouldBeIgnored.cs");

            var objDir = Path.Combine(testAsset.Path, "obj", "Debug", ToolsetInfo.CurrentTargetFramework);
            var objDirFilePath = Path.Combine(objDir, "ShouldBeIgnored.cs");

            Directory.CreateDirectory(binDir);

            App.Start(testAsset, []);

            await App.AssertWaitingForChanges();
            App.AssertOutputContains($"dotnet watch ⌚ Excluded directory: '{binDir}'");
            App.AssertOutputContains($"dotnet watch ⌚ Excluded directory: '{objDir}'");
            App.Process.ClearOutput();

            UpdateSourceFile(binDirFilePath, "class X;");
            UpdateSourceFile(objDirFilePath, "class X;");

            await App.WaitUntilOutputContains($"dotnet watch ⌚ Ignoring change in output directory: Add '{binDirFilePath}'");
            await App.WaitUntilOutputContains($"dotnet watch ⌚ Ignoring change in output directory: Add '{objDirFilePath}'");
        }

        [Fact]
        public async Task ProjectChange_GlobalUsings()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp")
                .WithSource();

            var programPath = Path.Combine(testAsset.Path, "Program.cs");
            var projectPath = Path.Combine(testAsset.Path, "WatchHotReloadApp.csproj");

            App.Start(testAsset, []);

            await App.AssertOutputLineStartsWith(MessageDescriptor.WaitingForChanges);

            // missing System.Linq import:
            UpdateSourceFile(programPath, content => content.Replace("""
                Console.WriteLine(".");
                """,
                """
                Console.WriteLine($">>> {typeof(XDocument)}");
                """));

            await App.AssertOutputLineStartsWith("dotnet watch ⌚ Unable to apply hot reload due to compilation errors.", failure: _ => false);

            UpdateSourceFile(projectPath, content => content.Replace("""
                <!-- items placeholder -->
                """,
                """
                <Using Include="System.Xml.Linq" />
                """));

            await App.AssertOutputLineStartsWith(MessageDescriptor.HotReloadSucceeded, $"WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})", failure: _ => false);

            await App.WaitUntilOutputContains(">>> System.Xml.Linq.XDocument");

            App.AssertOutputContains(MessageDescriptor.ReEvaluationCompleted);
        }

        [Theory]
        [CombinatorialData]
        public async Task AutoRestartOnRudeEdit(bool nonInteractive)
        {
            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp")
                .WithSource();

            if (!nonInteractive)
            {
                testAsset = testAsset
                    .WithProjectChanges(project =>
                    {
                        project.Root.Descendants()
                            .First(e => e.Name.LocalName == "PropertyGroup")
                            .Add(XElement.Parse("""
                                <HotReloadAutoRestart>true</HotReloadAutoRestart>
                                """));
                    });
            }

            var programPath = Path.Combine(testAsset.Path, "Program.cs");

            App.Start(testAsset, nonInteractive ? ["--non-interactive"] : []);

            await App.AssertWaitingForChanges();
            App.Process.ClearOutput();

            // rude edit: adding virtual method
            UpdateSourceFile(programPath, src => src.Replace("/* member placeholder */", "public virtual void F() {}"));

            await App.AssertOutputLineStartsWith(MessageDescriptor.WaitingForChanges, failure: _ => false);

            App.AssertOutputContains("⌚ Restart is needed to apply the changes");
            App.AssertOutputContains($"⌚ [auto-restart] {programPath}(38,11): error ENC0023: Adding an abstract method or overriding an inherited method requires restarting the application.");
            App.AssertOutputContains($"[WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Exited");
            App.AssertOutputContains($"[WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Launched");
            App.Process.ClearOutput();

            // valid edit:
            UpdateSourceFile(programPath, src => src.Replace("public virtual void F() {}", "public virtual void F() { Console.WriteLine(1); }"));

            await App.AssertOutputLineStartsWith($"dotnet watch 🔥 [WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Hot reload succeeded.");
        }

        [Fact]
        public async Task AutoRestartOnRudeEditAfterRestartPrompt()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp")
                .WithSource();

            var programPath = Path.Combine(testAsset.Path, "Program.cs");

            App.Start(testAsset, [], testFlags: TestFlags.ReadKeyFromStdin);

            await App.AssertWaitingForChanges();
            App.Process.ClearOutput();

            // rude edit: adding virtual method
            UpdateSourceFile(programPath, src => src.Replace("/* member placeholder */", "public virtual void F() {}"));

            await App.AssertOutputLineStartsWith("  ❔ Do you want to restart your app? Yes (y) / No (n) / Always (a) / Never (v)", failure: _ => false);

            App.AssertOutputContains("⌚ Restart is needed to apply the changes.");
            App.AssertOutputContains($"❌ {programPath}(38,11): error ENC0023: Adding an abstract method or overriding an inherited method requires restarting the application.");
            App.Process.ClearOutput();

            App.SendKey('a');

            await App.AssertOutputLineStartsWith(MessageDescriptor.WaitingForChanges, failure: _ => false);

            App.AssertOutputContains($"[WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Exited");
            App.AssertOutputContains($"[WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Launched");
            App.Process.ClearOutput();

            // rude edit: deleting virtual method
            UpdateSourceFile(programPath, src => src.Replace("public virtual void F() {}", ""));

            await App.AssertOutputLineStartsWith(MessageDescriptor.WaitingForChanges, failure: _ => false);

            App.AssertOutputContains("⌚ Restart is needed to apply the changes");
            App.AssertOutputContains($"⌚ [auto-restart] {programPath}(38,1): error ENC0033: Deleting method 'F()' requires restarting the application.");
            App.AssertOutputContains($"[WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Exited");
            App.AssertOutputContains($"[WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Launched");
        }

        [Theory]
        [CombinatorialData]
        public async Task AutoRestartOnNoEffectEdit(bool nonInteractive)
        {
            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp")
                .WithSource();

            if (!nonInteractive)
            {
                testAsset = testAsset
                    .WithProjectChanges(project =>
                    {
                        project.Root.Descendants()
                            .First(e => e.Name.LocalName == "PropertyGroup")
                            .Add(XElement.Parse("""
                                <HotReloadAutoRestart>true</HotReloadAutoRestart>
                                """));
                    });
            }

            var programPath = Path.Combine(testAsset.Path, "Program.cs");

            App.Start(testAsset, nonInteractive ? ["--non-interactive"] : []);

            await App.AssertWaitingForChanges();
            App.Process.ClearOutput();

            // top-level code change:
            UpdateSourceFile(programPath, src => src.Replace("Started", "<Updated>"));

            await App.AssertOutputLineStartsWith(MessageDescriptor.WaitingForChanges, failure: _ => false);

            App.AssertOutputContains("⌚ Restart is needed to apply the changes");
            App.AssertOutputContains($"⌚ [auto-restart] {programPath}(16,19): warning ENC0118: Changing 'top-level code' might not have any effect until the application is restarted.");
            App.AssertOutputContains($"[WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Exited");
            App.AssertOutputContains($"[WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Launched");
            App.AssertOutputContains("<Updated>");
            App.Process.ClearOutput();

            // valid edit:
            UpdateSourceFile(programPath, src => src.Replace("/* member placeholder */", "public void F() {}"));

            await App.AssertOutputLineStartsWith($"dotnet watch 🔥 [WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Hot reload succeeded.");
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

            await App.AssertOutputLineStartsWith("<Updated>", failure: _ => false);
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
                $"dotnet watch ⚠ [WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Expected to find a static method 'ClearCache', 'UpdateApplication' or 'UpdateContent' on type 'AppUpdateHandler, WatchHotReloadApp, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null' but neither exists.");
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

            await App.WaitUntilOutputContains($"dotnet watch ⚠ [WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Exception from 'AppUpdateHandler.ClearCache': System.InvalidOperationException: Bug!");

            if (verbose)
            {
                await App.WaitUntilOutputContains($"dotnet watch 🕵️ [WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Deltas applied.");
            }
            else
            {
                // shouldn't see any agent messages:
                App.AssertOutputDoesNotContain("🕵️");
            }
        }

        [PlatformSpecificTheory(TestPlatforms.Windows)] // https://github.com/dotnet/sdk/issues/49307
        [CombinatorialData]
        public async Task BlazorWasm(bool projectSpecifiesCapabilities)
        {
            var testAsset = TestAssets.CopyTestAsset("WatchBlazorWasm", identifier: projectSpecifiesCapabilities.ToString())
                .WithSource();

            if (projectSpecifiesCapabilities)
            {
                testAsset = testAsset.WithProjectChanges(proj =>
                {
                    proj.Root.Descendants()
                        .First(e => e.Name.LocalName == "PropertyGroup")
                        .Add(XElement.Parse("""
                            <WebAssemblyHotReloadCapabilities>Baseline;AddMethodToExistingType</WebAssemblyHotReloadCapabilities>
                            """));
                });
            }

            var port = TestOptions.GetTestPort();
            App.Start(testAsset, ["--urls", "http://localhost:" + port], testFlags: TestFlags.MockBrowser);

            await App.AssertWaitingForChanges();

            App.AssertOutputContains(MessageDescriptor.ConfiguredToUseBrowserRefresh);
            App.AssertOutputContains(MessageDescriptor.ConfiguredToLaunchBrowser);

            // Browser is launched based on blazor-devserver output "Now listening on: ...".
            await App.WaitUntilOutputContains($"dotnet watch ⌚ Launching browser: http://localhost:{port}");

            // Middleware should have been loaded to blazor-devserver before the browser is launched:
            App.AssertOutputContains("dbug: Microsoft.AspNetCore.Watch.BrowserRefresh.BlazorWasmHotReloadMiddleware[0]");
            App.AssertOutputContains("dbug: Microsoft.AspNetCore.Watch.BrowserRefresh.BrowserScriptMiddleware[0]");
            App.AssertOutputContains("Middleware loaded. Script /_framework/aspnetcore-browser-refresh.js");
            App.AssertOutputContains("Middleware loaded. Script /_framework/blazor-hotreload.js");
            App.AssertOutputContains("dbug: Microsoft.AspNetCore.Watch.BrowserRefresh.BrowserRefreshMiddleware");
            App.AssertOutputContains("Middleware loaded: DOTNET_MODIFIABLE_ASSEMBLIES=debug, __ASPNETCORE_BROWSER_TOOLS=true");

            // shouldn't see any agent messages (agent is not loaded into blazor-devserver):
            App.AssertOutputDoesNotContain("🕵️");

            var newSource = """
                @page "/"
                <h1>Updated</h1>
                """;

            UpdateSourceFile(Path.Combine(testAsset.Path, "Pages", "Index.razor"), newSource);
            await App.AssertOutputLineStartsWith(MessageDescriptor.HotReloadSucceeded, $"blazorwasm ({ToolsetInfo.CurrentTargetFramework})");

            // check project specified capapabilities:
            if (projectSpecifiesCapabilities)
            {
                App.AssertOutputContains("dotnet watch 🔥 Hot reload capabilities: Baseline AddMethodToExistingType.");
            }
            else
            {
                App.AssertOutputContains("dotnet watch 🔥 Hot reload capabilities: Baseline AddMethodToExistingType AddStaticFieldToExistingType NewTypeDefinition ChangeCustomAttributes AddInstanceFieldToExistingType GenericAddMethodToExistingType GenericUpdateMethod UpdateParameters GenericAddFieldToExistingType.");
            }
        }

        [PlatformSpecificFact(TestPlatforms.Windows)] // "https://github.com/dotnet/sdk/issues/49307")
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

        [PlatformSpecificFact(TestPlatforms.Windows)] // "https://github.com/dotnet/sdk/issues/49307")
        public async Task BlazorWasm_Restart()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchBlazorWasm")
                .WithSource();

            var port = TestOptions.GetTestPort();
            App.Start(testAsset, ["--urls", "http://localhost:" + port], testFlags: TestFlags.ReadKeyFromStdin | TestFlags.MockBrowser);

            await App.AssertWaitingForChanges();

            App.AssertOutputContains(MessageDescriptor.ConfiguredToUseBrowserRefresh);
            App.AssertOutputContains(MessageDescriptor.ConfiguredToLaunchBrowser);

            // Browser is launched based on blazor-devserver output "Now listening on: ...".
            await App.WaitUntilOutputContains($"dotnet watch ⌚ Launching browser: http://localhost:{port}");

            App.SendControlR();

            await App.WaitUntilOutputContains($"dotnet watch ⌚ Reloading browser.");
        }

        [PlatformSpecificFact(TestPlatforms.Windows)] // "https://github.com/dotnet/sdk/issues/49307")
        public async Task BlazorWasmHosted()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchBlazorWasmHosted")
                .WithSource();

            var tfm = ToolsetInfo.CurrentTargetFramework;

            var port = TestOptions.GetTestPort();
            App.Start(testAsset, ["--urls", "http://localhost:" + port], "blazorhosted", testFlags: TestFlags.MockBrowser);

            await App.AssertWaitingForChanges();

            App.AssertOutputContains(MessageDescriptor.ConfiguredToUseBrowserRefresh);
            App.AssertOutputContains(MessageDescriptor.ConfiguredToLaunchBrowser);
            App.AssertOutputContains("dotnet watch 🔥 HotReloadProfile: BlazorHosted");

            // client capabilities:
            App.AssertOutputContains($"dotnet watch ⌚ [blazorhosted ({tfm})] Project 'blazorwasm ({tfm})' specifies capabilities: 'Baseline AddMethodToExistingType AddStaticFieldToExistingType NewTypeDefinition ChangeCustomAttributes AddInstanceFieldToExistingType GenericAddMethodToExistingType GenericUpdateMethod UpdateParameters GenericAddFieldToExistingType'");

            // server capabilities:
            App.AssertOutputContains($"dotnet watch ⌚ [blazorhosted ({tfm})] Capabilities: 'Baseline AddMethodToExistingType AddStaticFieldToExistingType AddInstanceFieldToExistingType NewTypeDefinition ChangeCustomAttributes UpdateParameters GenericUpdateMethod GenericAddMethodToExistingType GenericAddFieldToExistingType AddFieldRva'");
        }

        [PlatformSpecificFact(TestPlatforms.Windows)] // "https://github.com/dotnet/sdk/issues/49307")
        public async Task Razor_Component_ScopedCssAndStaticAssets()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchRazorWithDeps")
                .WithSource();

            var port = TestOptions.GetTestPort();
            App.Start(testAsset, ["--urls", "http://localhost:" + port], relativeProjectDirectory: "RazorApp", testFlags: TestFlags.MockBrowser);

            await App.AssertWaitingForChanges();

            App.AssertOutputContains(MessageDescriptor.ConfiguredToUseBrowserRefresh);
            App.AssertOutputContains(MessageDescriptor.ConfiguredToLaunchBrowser);
            App.AssertOutputContains($"dotnet watch ⌚ Launching browser: http://localhost:{port}");
            App.Process.ClearOutput();

            var scopedCssPath = Path.Combine(testAsset.Path, "RazorClassLibrary", "Components", "Example.razor.css");

            var newCss = """
                .example {
                    color: blue;
                }
                """;

            UpdateSourceFile(scopedCssPath, newCss);
            await App.AssertOutputLineStartsWith("dotnet watch 🔥 Hot reload change handled");

            App.AssertOutputContains($"dotnet watch ⌚ Sending static asset update request to browser: 'RazorApp.css'.");
            App.AssertOutputContains($"dotnet watch 🔥 Hot reload of scoped css succeeded.");
            App.AssertOutputContains(MessageDescriptor.NoCSharpChangesToApply);
            App.Process.ClearOutput();

            var cssPath = Path.Combine(testAsset.Path, "RazorApp", "wwwroot", "app.css");
            UpdateSourceFile(cssPath, content => content.Replace("background-color: white;", "background-color: red;"));

            await App.AssertOutputLineStartsWith("dotnet watch 🔥 Hot reload change handled");

            // "wwwroot" directory is required for MAUI. Web sites work with or without it.
            App.AssertOutputContains($"dotnet watch ⌚ Sending static asset update request to browser: 'wwwroot/app.css'.");
            App.AssertOutputContains($"dotnet watch 🔥 Hot reload of static files succeeded.");
            App.AssertOutputContains(MessageDescriptor.NoCSharpChangesToApply);
            App.Process.ClearOutput();
        }

        /// <summary>
        /// Currently only works on Windows.
        /// Add TestPlatforms.OSX once https://github.com/dotnet/sdk/issues/45521 is fixed.
        /// </summary>
        [PlatformSpecificFact(TestPlatforms.Windows, Skip = "https://github.com/dotnet/sdk/issues/40006")]
        public async Task MauiBlazor()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchMauiBlazor")
                .WithSource();

            var workloadInstallCommandSpec = new DotnetCommand(Logger, ["workload", "install", "maui", "--include-previews"])
            {
                WorkingDirectory = testAsset.Path,
            };

            var result = workloadInstallCommandSpec.Execute();
            Assert.Equal(0, result.ExitCode);

            var platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows10.0.19041.0" : "maccatalyst";
            var tfm = $"{ToolsetInfo.CurrentTargetFramework}-{platform}";
            App.Start(testAsset, ["-f", tfm]);

            await App.AssertWaitingForChanges();

            // update code file:
            var razorPath = Path.Combine(testAsset.Path, "Components", "Pages", "Home.razor");
            UpdateSourceFile(razorPath, content => content.Replace("Hello, world!", "Updated"));

            await App.AssertOutputLineStartsWith("dotnet watch 🔥 Hot reload change handled");

            // TODO: Warning is currently reported because UpdateContent is not recognized
            App.AssertOutputContains("Updates applied: 1 out of 1.");
            App.AssertOutputContains("Microsoft.AspNetCore.Components.HotReload.HotReloadManager.UpdateApplication");
            App.Process.ClearOutput();

            // update static asset:
            var cssPath = Path.Combine(testAsset.Path, "wwwroot", "css", "app.css");
            UpdateSourceFile(cssPath, content => content.Replace("background-color: white;", "background-color: red;"));

            await App.AssertOutputLineStartsWith("dotnet watch 🔥 Hot reload change handled");
            App.AssertOutputContains("Updates applied: 1 out of 1.");
            App.AssertOutputContains("Microsoft.AspNetCore.Components.WebView.StaticContentHotReloadManager.UpdateContent");
            App.AssertOutputContains("No C# changes to apply.");
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
            Log("RenameSourceFile started");

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

            Log($"Renamed '{oldFilePath}' to '{newFilePath}'.");

            await App.AssertOutputLineStartsWith("> Renamed.cs");
        }

        [Theory]
        [InlineData(true, Skip = "https://github.com/dotnet/sdk/issues/43320")]
        [InlineData(false)]
        public async Task RenameDirectory(bool useMove)
        {
            Log("RenameSourceFile started");

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

            App.Start(testAsset, ["--non-interactive"], "AppWithDeps");

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

            Log($"Renamed '{oldSubdir}' to '{newSubdir}'.");

            // dotnet-watch may observe the delete separately from the new file write.
            // If so, rude edit is reported, the app is auto-restarted and we should observe the final result.

            await App.AssertOutputLineStartsWith("> NewSubdir", failure: _ => false);
        }

        [PlatformSpecificFact(TestPlatforms.Windows)] // "https://github.com/dotnet/sdk/issues/49307")
        public async Task Aspire_BuildError_ManualRestart()
        {
            var tfm = ToolsetInfo.CurrentTargetFramework;
            var testAsset = TestAssets.CopyTestAsset("WatchAspire")
                .WithSource();

            var serviceSourcePath = Path.Combine(testAsset.Path, "WatchAspire.ApiService", "Program.cs");
            var serviceProjectPath = Path.Combine(testAsset.Path, "WatchAspire.ApiService", "WatchAspire.ApiService.csproj");
            var serviceSource = File.ReadAllText(serviceSourcePath, Encoding.UTF8);

            var webSourcePath = Path.Combine(testAsset.Path, "WatchAspire.Web", "Program.cs");
            var webProjectPath = Path.Combine(testAsset.Path, "WatchAspire.Web", "WatchAspire.Web.csproj");

            App.Start(testAsset, ["-lp", "http"], relativeProjectDirectory: "WatchAspire.AppHost", testFlags: TestFlags.ReadKeyFromStdin);

            await App.AssertWaitingForChanges();

            // check that Aspire server output is logged via dotnet-watch reporter:
            await App.WaitUntilOutputContains("dotnet watch ⭐ Now listening on:");

            // wait until after DCP session started:
            await App.WaitUntilOutputContains("dotnet watch ⭐ Session started: #1");

            // Service -- valid code change:
            UpdateSourceFile(
                serviceSourcePath,
                serviceSource.Replace("Enumerable.Range(1, 5)", "Enumerable.Range(1, 10)"));

            await App.AssertOutputLineStartsWith("dotnet watch 🔥 Hot reload change handled");

            App.AssertOutputContains("Using Aspire process launcher.");
            App.AssertOutputContains(MessageDescriptor.HotReloadSucceeded, $"WatchAspire.AppHost ({tfm})");
            App.AssertOutputContains(MessageDescriptor.HotReloadSucceeded, $"WatchAspire.ApiService ({tfm})");
            App.AssertOutputContains(MessageDescriptor.HotReloadSucceeded, $"WatchAspire.Web ({tfm})");

            // Only one browser should be launched (dashboard). The child process shouldn't launch a browser.
            Assert.Equal(1, App.Process.Output.Count(line => line.StartsWith("dotnet watch ⌚ Launching browser: ")));
            App.Process.ClearOutput();

            // rude edit with build error:
            UpdateSourceFile(
                serviceSourcePath,
                serviceSource.Replace("record WeatherForecast", "record WeatherForecast2"));

            await App.AssertOutputLineStartsWith("  ❔ Do you want to restart these projects? Yes (y) / No (n) / Always (a) / Never (v)");

            App.AssertOutputContains("dotnet watch ⌚ Restart is needed to apply the changes.");
            App.AssertOutputContains($"dotnet watch ❌ {serviceSourcePath}(36,1): error ENC0020: Renaming record 'WeatherForecast' requires restarting the application.");
            App.AssertOutputContains("dotnet watch ⌚ Affected projects:");
            App.AssertOutputContains("dotnet watch ⌚   WatchAspire.ApiService");
            App.Process.ClearOutput();

            App.SendKey('y');

            await App.AssertOutputLineStartsWith(MessageDescriptor.FixBuildError, failure: _ => false);

            // We don't have means to gracefully terminate process on Windows, see https://github.com/dotnet/runtime/issues/109432
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                App.AssertOutputContains($"dotnet watch ❌ [WatchAspire.ApiService ({tfm})] Exited with error code -1");
            }
            else
            {
                // Unix process may return exit code = 128 + SIGTERM
                // Exited with error code 143
                App.AssertOutputContains($"[WatchAspire.ApiService ({tfm})] Exited");
            }

            App.AssertOutputContains($"dotnet watch ⌚ Building {serviceProjectPath} ...");
            App.AssertOutputContains("error CS0246: The type or namespace name 'WeatherForecast' could not be found");
            App.Process.ClearOutput();

            // fix build error:
            UpdateSourceFile(
                serviceSourcePath,
                serviceSource.Replace("WeatherForecast", "WeatherForecast2"));

            await App.AssertOutputLineStartsWith($"dotnet watch ⌚ [WatchAspire.ApiService ({tfm})] Capabilities");

            App.AssertOutputContains($"dotnet watch 🔨 Build succeeded: {serviceProjectPath}");
            App.AssertOutputContains("dotnet watch 🔥 Projects rebuilt");
            App.AssertOutputContains($"dotnet watch ⭐ Starting project: {serviceProjectPath}");

            // Note: sending Ctrl+C via standard input is not the same as sending real Ctrl+C.
            // The latter terminates the processes gracefully on Windows, so exit codes -1 are actually not reported.
            App.SendControlC();

            await App.AssertOutputLineStartsWith("dotnet watch 🛑 Shutdown requested. Press Ctrl+C again to force exit.");

            // We don't have means to gracefully terminate process on Windows, see https://github.com/dotnet/runtime/issues/109432
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await App.AssertOutputLineStartsWith($"dotnet watch ❌ [WatchAspire.ApiService ({tfm})] Exited with error code -1");
                await App.AssertOutputLineStartsWith($"dotnet watch ❌ [WatchAspire.AppHost ({tfm})] Exited with error code -1");
            }
            else
            {
                // Unix process may return exit code = 128 + SIGTERM
                // Exited with error code 143
                await App.AssertOutputLine(line => line.Contains($"[WatchAspire.ApiService ({tfm})] Exited"));
                await App.AssertOutputLine(line => line.Contains($"[WatchAspire.AppHost ({tfm})] Exited"));
            }

            await App.AssertOutputLineStartsWith("dotnet watch ⭐ Waiting for server to shutdown ...");

            App.AssertOutputContains("dotnet watch ⭐ Stop session #1");
            App.AssertOutputContains("dotnet watch ⭐ [#1] Sending 'sessionTerminated'");
        }

        [PlatformSpecificFact(TestPlatforms.Windows)] // "https://github.com/dotnet/sdk/issues/49307")
        public async Task Aspire_NoEffect_AutoRestart()
        {
            var tfm = ToolsetInfo.CurrentTargetFramework;
            var testAsset = TestAssets.CopyTestAsset("WatchAspire")
                .WithSource();

            var webSourcePath = Path.Combine(testAsset.Path, "WatchAspire.Web", "Program.cs");
            var webProjectPath = Path.Combine(testAsset.Path, "WatchAspire.Web", "WatchAspire.Web.csproj");
            var webSource = File.ReadAllText(webSourcePath, Encoding.UTF8);

            App.Start(testAsset, ["-lp", "http", "--non-interactive"], relativeProjectDirectory: "WatchAspire.AppHost");

            await App.AssertWaitingForChanges();

            // wait until after DCP sessions have been started for all projects:
            await App.WaitUntilOutputContains("dotnet watch ⭐ Session started: #2");
            App.Process.ClearOutput();

            // no-effect edit:
            UpdateSourceFile(webSourcePath, src => src.Replace("/* top-level placeholder */", "builder.Services.AddRazorComponents();"));

            await App.AssertOutputLineStartsWith("dotnet watch 🔥 Hot reload change handled", _ => false);
            await App.WaitUntilOutputContains("dotnet watch ⭐ Session started: #2");

            App.AssertOutputContains("dotnet watch 🔥 Projects restarted (1)");
            App.AssertOutputDoesNotContain("⚠");

            App.Process.ClearOutput();

            // lambda body edit:
            UpdateSourceFile(webSourcePath, src => src.Replace("Hello world!", "<Updated>"));

            await App.AssertOutputLineStartsWith("dotnet watch 🔥 Hot reload change handled");
            App.AssertOutputContains($"dotnet watch 🕵️ [WatchAspire.Web ({tfm})] Deltas applied.");
            App.AssertOutputDoesNotContain("Projects rebuilt");
            App.AssertOutputDoesNotContain("Projects restarted");
            App.AssertOutputDoesNotContain("⚠");
        }
    }
}
