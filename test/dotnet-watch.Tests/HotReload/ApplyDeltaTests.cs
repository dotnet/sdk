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

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

            // add a new file:
            UpdateSourceFile(Path.Combine(dependencyDir, "AnotherLib.cs"), """
                public class AnotherLib
                {
                    public static void Print()
                        => System.Console.WriteLine("Changed!");
                }
                """);

            await App.WaitUntilOutputContains(MessageDescriptor.ReEvaluationCompleted);

            // update existing file:
            UpdateSourceFile(Path.Combine(dependencyDir, "Foo.cs"), """
                public class Lib
                {
                    public static void Print()
                        => AnotherLib.Print();
                }
                """);

            await App.WaitUntilOutputContains("Changed!");
        }

        [Fact]
        public async Task ChangeFileInDependency()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchAppWithProjectDeps")
                .WithSource();

            var dependencyDir = Path.Combine(testAsset.Path, "Dependency");

            App.Start(testAsset, [], "AppWithDeps");

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

            var newSrc = """
                public class Lib
                {
                    public static void Print()
                        => System.Console.WriteLine("Changed!");
                }
                """;

            UpdateSourceFile(Path.Combine(dependencyDir, "Foo.cs"), newSrc);

            await App.WaitUntilOutputContains("dotnet watch 🔥 Hot reload capabilities: AddExplicitInterfaceImplementation AddFieldRva AddInstanceFieldToExistingType AddMethodToExistingType AddStaticFieldToExistingType Baseline ChangeCustomAttributes GenericAddFieldToExistingType GenericAddMethodToExistingType GenericUpdateMethod NewTypeDefinition UpdateParameters.");

            await App.WaitUntilOutputContains("Changed!");
        }

        [Fact]
        public async Task ProjectChange_UpdateDirectoryBuildPropsThenUpdateSource()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchAppWithProjectDeps")
                .WithSource();

            var dependencyDir = Path.Combine(testAsset.Path, "Dependency");

            App.Start(testAsset, [], "AppWithDeps");

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

            UpdateSourceFile(
                Path.Combine(testAsset.Path, "Directory.Build.props"),
                src => src.Replace("<AllowUnsafeBlocks>false</AllowUnsafeBlocks>", "<AllowUnsafeBlocks>true</AllowUnsafeBlocks>"));

            await App.WaitUntilOutputContains(MessageDescriptor.NoCSharpChangesToApply);
            await App.WaitUntilOutputContains(MessageDescriptor.ProjectChangeTriggeredReEvaluation);

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

            await App.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);
            await App.WaitUntilOutputContains("Changed!");
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

            App.Start(testAsset, ["--non-interactive"], "AppWithDeps");

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
            await App.WaitUntilOutputContains($"{symbolName} set");
            App.Process.ClearOutput();

            UpdateSourceFile(buildFilePath, src => src.Replace(symbolName, ""));

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
            await App.WaitUntilOutputContains(MessageDescriptor.ProjectChangeTriggeredReEvaluation);
            await App.WaitUntilOutputContains("dotnet watch ⌚ [auto-restart] error ENC1102: Changing project setting 'DefineConstants'");

            await App.WaitUntilOutputContains($"{symbolName} not set");
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

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
            await App.WaitUntilOutputContains("BUILD_CONST_IN_PROPS set");
            App.Process.ClearOutput();

            UpdateSourceFile(
                directoryBuildProps,
                src => src.Replace("BUILD_CONST_IN_PROPS", ""));

            await App.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);
            await App.WaitUntilOutputContains("BUILD_CONST not set");

            await App.WaitUntilOutputContains(MessageDescriptor.ProjectChangeTriggeredReEvaluation);
        }

        [Fact]
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

            App.Start(testAsset, ["--non-interactive"], "AppWithDeps");

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
            await App.WaitUntilOutputContains("BUILD_CONST_IN_PROPS set");

            // delete Directory.Build.props that defines BUILD_CONST_IN_PROPS
            Log($"Deleting {directoryBuildProps}");
            File.Delete(directoryBuildProps);

            // Project needs to be re-evaluated:
            await App.WaitUntilOutputContains(MessageDescriptor.ProjectChangeTriggeredReEvaluation);
            App.Process.ClearOutput();

            await App.WaitUntilOutputContains("BUILD_CONST_IN_PROPS not set");
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

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
            await App.WaitUntilOutputContains(new Regex(@"dotnet watch ⌚ Exclusion glob: 'AppData/[*][*]/[*][.][*];bin[/\\]+Debug[/\\]+[*][*];obj[/\\]+Debug[/\\]+[*][*];bin[/\\]+[*][*];obj[/\\]+[*][*]"));
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

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
            await App.WaitUntilOutputContains($"dotnet watch ⌚ Excluded directory: '{binDir}'");
            await App.WaitUntilOutputContains($"dotnet watch ⌚ Excluded directory: '{objDir}'");
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

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

            // missing System.Linq import:
            UpdateSourceFile(programPath, content => content.Replace("""
                Console.WriteLine(".");
                """,
                """
                Console.WriteLine($">>> {typeof(XDocument)}");
                """));

            await App.WaitUntilOutputContains(MessageDescriptor.UnableToApplyChanges);
            App.Process.ClearOutput();

            UpdateSourceFile(projectPath, content => content.Replace("""
                <!-- items placeholder -->
                """,
                """
                <Using Include="System.Xml.Linq" />
                """));

            await App.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);

            await App.WaitUntilOutputContains(">>> System.Xml.Linq.XDocument");

            await App.WaitUntilOutputContains(MessageDescriptor.ReEvaluationCompleted);
        }

        [Fact]
        public async Task BinaryLogs()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp")
                .WithSource();

            var projectPath = Path.Combine(testAsset.Path, "WatchHotReloadApp.csproj");
            var logDir = Path.Combine(testAsset.Path, "logs");
            var binLogPath = Path.Combine(logDir, "Test.binlog");
            var binLogPathBase = Path.ChangeExtension(binLogPath, "").TrimEnd('.');

            Assert.False(Directory.Exists(logDir));

            App.SuppressVerboseLogging();
            App.Start(testAsset, ["--verbose", $"-bl:{binLogPath}"], testFlags: TestFlags.None);

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

            var expectedLogs = new List<string>()
            {
                // dotnet build log
                binLogPath,
                // dotnet run log
                binLogPathBase + "-dotnet-run.binlog",
                // initial DTB:
                binLogPathBase + "-dotnet-watch.DesignTimeBuild.WatchHotReloadApp.csproj.1.binlog"
            };

            VerifyExpectedLogFiles();

            UpdateSourceFile(projectPath, content => content.Replace("""
                <!-- items placeholder -->
                """,
                """
                <Using Include="System.Xml.Linq" />
                """));

            await App.WaitUntilOutputContains(MessageDescriptor.ReEvaluationCompleted);

            // project update triggered restore and DTB:
            expectedLogs.Add(binLogPathBase + "-dotnet-watch.Restore.WatchHotReloadApp.csproj.2.binlog");
            expectedLogs.Add(binLogPathBase + "-dotnet-watch.DesignTimeBuild.WatchHotReloadApp.csproj.3.binlog");

            VerifyExpectedLogFiles();

            void VerifyExpectedLogFiles()
            {
                AssertEx.SequenceEqual(
                    expectedLogs.Order(),
                    Directory.EnumerateFileSystemEntries(logDir, "*.*", SearchOption.AllDirectories).Order());
            }
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

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
            App.Process.ClearOutput();

            // rude edit: adding virtual method
            UpdateSourceFile(programPath, src => src.Replace("/* member placeholder */", "public virtual void F() {}"));

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

            await App.WaitUntilOutputContains(MessageDescriptor.RestartNeededToApplyChanges);
            await App.WaitUntilOutputContains($"⌚ [auto-restart] {programPath}(39,11): error ENC0023: Adding an abstract method or overriding an inherited method requires restarting the application.");
            await App.WaitUntilOutputContains($"[WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Exited");
            await App.WaitUntilOutputContains($"[WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Launched");
            App.Process.ClearOutput();

            // valid edit:
            UpdateSourceFile(programPath, src => src.Replace("public virtual void F() {}", "public virtual void F() { Console.WriteLine(1); }"));

            await App.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);
        }

        [Theory(Skip = "https://github.com/dotnet/sdk/issues/51469")]
        [CombinatorialData]
        public async Task AutoRestartOnRuntimeRudeEdit(bool nonInteractive)
        {
            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp")
                .WithSource();

            var tfm = ToolsetInfo.CurrentTargetFramework;
            var programPath = Path.Combine(testAsset.Path, "Program.cs");

            // Changes the type of lambda without updating top-level code.
            // The loop will end up calling the old version of the lambda resulting in runtime rude edit.

            File.WriteAllText(programPath, """
                using System;
                using System.Threading;

                var d = C.F();

                while (true)
                {
                    Thread.Sleep(250);
                    d(1);
                }

                class C
                {
                    public static Action<int> F()
                    {
                        return a =>
                        {
                            Console.WriteLine(a.GetType());
                        };
                    }
                }
                """);

            App.Start(testAsset, nonInteractive ? ["--non-interactive"] : []);

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
            await App.WaitUntilOutputContains("System.Int32");
            App.Process.ClearOutput();

            UpdateSourceFile(programPath, src => src.Replace("Action<int>", "Action<byte>"));

            // The following agent messages must be reported in order.
            // The HotReloadException handler needs to be installed and update handlers invoked and completed before the
            // HotReloadException handler may proceed with runtime rude edit processing and application restart.
            await App.WaitForOutputLineContaining($"dotnet watch 🕵️ [WatchHotReloadApp ({tfm})] HotReloadException handler installed.");
            await App.WaitForOutputLineContaining($"dotnet watch 🕵️ [WatchHotReloadApp ({tfm})] Invoking metadata update handlers.");
            await App.WaitForOutputLineContaining($"dotnet watch 🕵️ [WatchHotReloadApp ({tfm})] Updates applied.");
            await App.WaitForOutputLineContaining($"dotnet watch 🕵️ [WatchHotReloadApp ({tfm})] Runtime rude edit detected:");

            await App.WaitUntilOutputContains($"dotnet watch ⚠ [WatchHotReloadApp ({tfm})] " +
                "Attempted to invoke a deleted lambda or local function implementation. " +
                "This can happen when lambda or local function is deleted while the application is running.");

            await App.WaitUntilOutputContains(MessageDescriptor.RestartingApplication, $"WatchHotReloadApp ({tfm})");

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
            await App.WaitUntilOutputContains("System.Byte");
        }

        [Fact]
        public async Task AutoRestartOnRudeEditAfterRestartPrompt()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp")
                .WithSource();

            var programPath = Path.Combine(testAsset.Path, "Program.cs");

            App.Start(testAsset, [], testFlags: TestFlags.ReadKeyFromStdin);

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
            App.Process.ClearOutput();

            // rude edit: adding virtual method
            UpdateSourceFile(programPath, src => src.Replace("/* member placeholder */", "public virtual void F() {}"));

            // the prompt is printed into stdout while the error is printed into stderr, so they might arrive in any order:
            await App.WaitUntilOutputContains("  ❔ Do you want to restart your app? Yes (y) / No (n) / Always (a) / Never (v)");
            await App.WaitUntilOutputContains(MessageDescriptor.RestartNeededToApplyChanges);

            await App.WaitUntilOutputContains($"❌ {programPath}(39,11): error ENC0023: Adding an abstract method or overriding an inherited method requires restarting the application.");
            App.Process.ClearOutput();

            App.SendKey('a');

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

            App.AssertOutputContains($"[WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Exited");
            App.AssertOutputContains($"[WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Launched");
            App.Process.ClearOutput();

            // rude edit: deleting virtual method
            UpdateSourceFile(programPath, src => src.Replace("public virtual void F() {}", ""));

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

            await App.WaitUntilOutputContains(MessageDescriptor.RestartNeededToApplyChanges);
            await App.WaitUntilOutputContains($"⌚ [auto-restart] {programPath}(39,1): error ENC0033: Deleting method 'F()' requires restarting the application.");
            await App.WaitUntilOutputContains($"[WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Exited");
            await App.WaitUntilOutputContains($"[WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Launched");
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

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
            App.Process.ClearOutput();

            // top-level code change:
            UpdateSourceFile(programPath, src => src.Replace("Started", "<Updated>"));

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

            await App.WaitUntilOutputContains(MessageDescriptor.RestartNeededToApplyChanges);
            await App.WaitUntilOutputContains($"⌚ [auto-restart] {programPath}(17,19): warning ENC0118: Changing 'top-level code' might not have any effect until the application is restarted.");
            await App.WaitUntilOutputContains($"[WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Exited");
            await App.WaitUntilOutputContains($"[WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Launched");
            await App.WaitUntilOutputContains("<Updated>");
            App.Process.ClearOutput();

            // valid edit:
            UpdateSourceFile(programPath, src => src.Replace("/* member placeholder */", "public void F() {}"));

            await App.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);
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

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForFileChangeBeforeRestarting);

            UpdateSourceFile(programPath, """
                System.Console.WriteLine("<Updated>");
                """);

            await App.WaitUntilOutputContains("<Updated>");
        }

        [Fact]
        public async Task ChangeFileInFSharpProject()
        {
            var testAsset = TestAssets.CopyTestAsset("FSharpTestAppSimple")
                .WithSource();

            App.Start(testAsset, []);

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForFileChangeBeforeRestarting);

            UpdateSourceFile(Path.Combine(testAsset.Path, "Program.fs"), content => content.Replace("Hello World!", "<Updated>"));

            await App.WaitUntilOutputContains("<Updated>");
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

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
            App.Process.ClearOutput();

            UpdateSourceFile(sourcePath, content => content.Replace("Waiting", "<Updated>"));

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
            await App.WaitUntilOutputContains("<Updated>");
            App.Process.ClearOutput();

            UpdateSourceFile(sourcePath, content => content.Replace("<Updated>", "<Updated2>"));

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
            await App.WaitUntilOutputContains("<Updated2>");
        }

        // Test is timing out on .NET Framework: https://github.com/dotnet/sdk/issues/41669
        [CoreMSBuildOnlyFact]
        public async Task HandleTypeLoadFailure()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchAppTypeLoadFailure")
                .WithSource();

            App.Start(testAsset, [], "App");

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

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

            await App.WaitUntilOutputContains("Updated types: Printer");
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

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

            UpdateSourceFile(sourcePath, source.Replace("Console.WriteLine(\".\");", "Console.WriteLine(\"<Updated>\");"));

            await App.WaitUntilOutputContains("<Updated>");

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
                App.SuppressVerboseLogging();
            }

            App.Start(testAsset, []);

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

            UpdateSourceFile(sourcePath, source.Replace("Console.WriteLine(\".\");", "Console.WriteLine(\"<Updated>\");"));

            await App.WaitUntilOutputContains("<Updated>");

            await App.WaitUntilOutputContains($"dotnet watch ⚠ [WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Exception from 'AppUpdateHandler.ClearCache': System.InvalidOperationException: Bug!");

            if (verbose)
            {
                await App.WaitUntilOutputContains(MessageDescriptor.UpdateBatchCompleted);
            }
            else
            {
                // shouldn't see any agent messages:
                App.AssertOutputDoesNotContain("🕵️");
            }
        }

        [PlatformSpecificFact(TestPlatforms.Windows)]
        public async Task GracefulTermination_Windows()
        {
            var tfm = ToolsetInfo.CurrentTargetFramework;

            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp")
               .WithSource();

            var programPath = Path.Combine(testAsset.Path, "Program.cs");

            UpdateSourceFile(programPath, src => src.Replace("// <metadata update handler placeholder>", """
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    Console.WriteLine("Ctrl+C detected! Performing cleanup...");
                    Environment.Exit(0);
                };
                """));

            App.Start(testAsset, [], testFlags: TestFlags.ReadKeyFromStdin);

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

            await App.WaitUntilOutputContains($"dotnet watch 🕵️ [WatchHotReloadApp ({tfm})] Windows Ctrl+C handling enabled.");

            await App.WaitUntilOutputContains("Started");

            App.SendControlC();

            await App.WaitUntilOutputContains("Ctrl+C detected! Performing cleanup...");
            await App.WaitUntilOutputContains("exited with exit code 0.");
        }

        [PlatformSpecificFact(TestPlatforms.AnyUnix)]
        public async Task GracefulTermination_Unix()
        {
            var tfm = ToolsetInfo.CurrentTargetFramework;

            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp")
               .WithSource();

            var programPath = Path.Combine(testAsset.Path, "Program.cs");

            UpdateSourceFile(programPath, src => src.Replace("// <metadata update handler placeholder>", """
                using var termSignalRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ =>
                {
                    Console.WriteLine("SIGTERM detected! Performing cleanup...");
                });
                """));

            App.Start(testAsset, [], testFlags: TestFlags.ReadKeyFromStdin);

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

            await App.WaitUntilOutputContains($"dotnet watch 🕵️ [WatchHotReloadApp ({tfm})] Posix signal handlers registered.");

            await App.WaitUntilOutputContains("Started");

            App.SendControlC();

            await App.WaitUntilOutputContains("SIGTERM detected! Performing cleanup...");
            await App.WaitUntilOutputContains("exited with exit code 0.");
        }

        [PlatformSpecificTheory(TestPlatforms.Windows, Skip = "https://github.com/dotnet/sdk/issues/49928")] // https://github.com/dotnet/aspnetcore/issues/63759
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

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

            await App.WaitUntilOutputContains(MessageDescriptor.ConfiguredToUseBrowserRefresh);
            await App.WaitUntilOutputContains(MessageDescriptor.ConfiguredToLaunchBrowser);

            // Browser is launched based on blazor-devserver output "Now listening on: ...".
            await App.WaitUntilOutputContains(MessageDescriptor.LaunchingBrowser.GetMessage($"http://localhost:{port}", ""));

            // Middleware should have been loaded to blazor-devserver before the browser is launched:
            await App.WaitUntilOutputContains("dbug: Microsoft.AspNetCore.Watch.BrowserRefresh.BlazorWasmHotReloadMiddleware[0]");
            await App.WaitUntilOutputContains("dbug: Microsoft.AspNetCore.Watch.BrowserRefresh.BrowserScriptMiddleware[0]");
            await App.WaitUntilOutputContains("Middleware loaded. Script /_framework/aspnetcore-browser-refresh.js");
            await App.WaitUntilOutputContains("Middleware loaded. Script /_framework/blazor-hotreload.js");
            await App.WaitUntilOutputContains("dbug: Microsoft.AspNetCore.Watch.BrowserRefresh.BrowserRefreshMiddleware");
            await App.WaitUntilOutputContains("Middleware loaded: DOTNET_MODIFIABLE_ASSEMBLIES=debug, __ASPNETCORE_BROWSER_TOOLS=true");

            // shouldn't see any agent messages (agent is not loaded into blazor-devserver):
            App.AssertOutputDoesNotContain("🕵️");

            var newSource = """
                @page "/"
                <h1>Updated</h1>
                """;

            UpdateSourceFile(Path.Combine(testAsset.Path, "Pages", "Index.razor"), newSource);
            await App.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);

            // check project specified capapabilities:
            if (projectSpecifiesCapabilities)
            {
                await App.WaitUntilOutputContains("dotnet watch 🔥 Hot reload capabilities: AddExplicitInterfaceImplementation AddMethodToExistingType Baseline.");
            }
            else
            {
                await App.WaitUntilOutputContains("dotnet watch 🔥 Hot reload capabilities: AddExplicitInterfaceImplementation AddFieldRva AddInstanceFieldToExistingType AddMethodToExistingType AddStaticFieldToExistingType Baseline ChangeCustomAttributes GenericAddFieldToExistingType GenericAddMethodToExistingType GenericUpdateMethod NewTypeDefinition UpdateParameters.");
            }
        }

        [PlatformSpecificFact(TestPlatforms.Windows)] // https://github.com/dotnet/aspnetcore/issues/63759
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

            await App.WaitUntilOutputContains("dotnet watch ⚠ msbuild: [Warning] Duplicate source file");
            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
        }

        [PlatformSpecificFact(TestPlatforms.Windows)] // https://github.com/dotnet/aspnetcore/issues/63759
        public async Task BlazorWasm_Restart()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchBlazorWasm")
                .WithSource();

            var port = TestOptions.GetTestPort();
            App.Start(testAsset, ["--urls", "http://localhost:" + port, "--non-interactive"], testFlags: TestFlags.ReadKeyFromStdin | TestFlags.MockBrowser);

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

            await App.WaitUntilOutputContains(MessageDescriptor.ConfiguredToUseBrowserRefresh);
            await App.WaitUntilOutputContains(MessageDescriptor.ConfiguredToLaunchBrowser);
            await App.WaitUntilOutputContains(MessageDescriptor.PressCtrlRToRestart);

            // Browser is launched based on blazor-devserver output "Now listening on: ...".
            await App.WaitUntilOutputContains(MessageDescriptor.LaunchingBrowser.GetMessage($"http://localhost:{port}", ""));

            App.SendControlR();

            await App.WaitUntilOutputContains(MessageDescriptor.ReloadingBrowser);
        }

        [PlatformSpecificFact(TestPlatforms.Windows, Skip = "https://github.com/dotnet/sdk/issues/49928")] // https://github.com/dotnet/aspnetcore/issues/63759
        public async Task BlazorWasmHosted()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchBlazorWasmHosted")
                .WithSource();

            var tfm = ToolsetInfo.CurrentTargetFramework;

            var port = TestOptions.GetTestPort();
            App.Start(testAsset, ["--urls", "http://localhost:" + port], "blazorhosted", testFlags: TestFlags.MockBrowser);

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

            await App.WaitUntilOutputContains(MessageDescriptor.ConfiguredToUseBrowserRefresh);
            await App.WaitUntilOutputContains(MessageDescriptor.ConfiguredToLaunchBrowser);
            await App.WaitUntilOutputContains(MessageDescriptor.ApplicationKind_BlazorHosted);

            // client capabilities:
            await App.WaitUntilOutputContains($"dotnet watch ⌚ [blazorhosted ({tfm})] Project specifies capabilities: Baseline AddMethodToExistingType AddStaticFieldToExistingType NewTypeDefinition ChangeCustomAttributes AddInstanceFieldToExistingType GenericAddMethodToExistingType GenericUpdateMethod UpdateParameters GenericAddFieldToExistingType AddExplicitInterfaceImplementation.");

            // server capabilities:
            await App.WaitUntilOutputContains($"dotnet watch ⌚ [blazorhosted ({tfm})] Capabilities: Baseline AddMethodToExistingType AddStaticFieldToExistingType AddInstanceFieldToExistingType NewTypeDefinition ChangeCustomAttributes UpdateParameters GenericUpdateMethod GenericAddMethodToExistingType GenericAddFieldToExistingType AddFieldRva AddExplicitInterfaceImplementation.");
        }

        [PlatformSpecificFact(TestPlatforms.Windows)] // https://github.com/dotnet/aspnetcore/issues/63759
        public async Task Razor_Component_ScopedCssAndStaticAssets()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchRazorWithDeps")
                .WithSource();

            var port = TestOptions.GetTestPort();
            App.Start(testAsset, ["--urls", "http://localhost:" + port], relativeProjectDirectory: "RazorApp", testFlags: TestFlags.MockBrowser);

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

            await App.WaitUntilOutputContains(MessageDescriptor.ConfiguredToUseBrowserRefresh);
            await App.WaitUntilOutputContains(MessageDescriptor.ConfiguredToLaunchBrowser);
            await App.WaitUntilOutputContains(MessageDescriptor.LaunchingBrowser.GetMessage($"http://localhost:{port}", ""));
            App.Process.ClearOutput();

            var scopedCssPath = Path.Combine(testAsset.Path, "RazorClassLibrary", "Components", "Example.razor.css");

            var newCss = """
                .example {
                    color: blue;
                }
                """;

            UpdateSourceFile(scopedCssPath, newCss);
            await App.WaitUntilOutputContains(MessageDescriptor.StaticAssetsChangesApplied);
            await App.WaitUntilOutputContains(MessageDescriptor.NoCSharpChangesToApply);

            await App.WaitUntilOutputContains(MessageDescriptor.SendingStaticAssetUpdateRequest.GetMessage("wwwroot/RazorClassLibrary.bundle.scp.css"));
            App.Process.ClearOutput();

            var cssPath = Path.Combine(testAsset.Path, "RazorApp", "wwwroot", "app.css");
            UpdateSourceFile(cssPath, content => content.Replace("background-color: white;", "background-color: red;"));

            await App.WaitUntilOutputContains(MessageDescriptor.StaticAssetsChangesApplied);
            await App.WaitUntilOutputContains(MessageDescriptor.NoCSharpChangesToApply);

            await App.WaitUntilOutputContains(MessageDescriptor.SendingStaticAssetUpdateRequest.GetMessage("wwwroot/app.css"));
            App.Process.ClearOutput();
        }

        /// <summary>
        /// Currently only works on Windows.
        /// Add TestPlatforms.OSX once https://github.com/dotnet/sdk/issues/45521 is fixed.
        /// </summary>
        [PlatformSpecificFact(TestPlatforms.Windows)]
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

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

            // update code file:
            var razorPath = Path.Combine(testAsset.Path, "Components", "Pages", "Home.razor");
            UpdateSourceFile(razorPath, content => content.Replace("Hello, world!", "Updated"));

            await App.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);

            await App.WaitUntilOutputContains("Microsoft.AspNetCore.Components.HotReload.HotReloadManager.UpdateApplication");
            App.Process.ClearOutput();

            // update static asset:
            var cssPath = Path.Combine(testAsset.Path, "wwwroot", "css", "app.css");
            UpdateSourceFile(cssPath, content => content.Replace("background-color: white;", "background-color: red;"));

            await App.WaitUntilOutputContains(MessageDescriptor.StaticAssetsChangesApplied);
            await App.WaitUntilOutputContains("Microsoft.AspNetCore.Components.WebView.StaticContentHotReloadManager.UpdateContent");
            await App.WaitUntilOutputContains(MessageDescriptor.NoCSharpChangesToApply);
            App.Process.ClearOutput();

            // update scoped css:
            var scopedCssPath = Path.Combine(testAsset.Path, "Components", "Pages", "Counter.razor.css");
            UpdateSourceFile(scopedCssPath, content => content.Replace("background-color: green", "background-color: red"));

            await App.WaitUntilOutputContains(MessageDescriptor.StaticAssetsChangesApplied);
            await App.WaitUntilOutputContains("Microsoft.AspNetCore.Components.WebView.StaticContentHotReloadManager.UpdateContent");
            await App.WaitUntilOutputContains(MessageDescriptor.NoCSharpChangesToApply);
        }

        // Test is timing out on .NET Framework: https://github.com/dotnet/sdk/issues/41669
        [CoreMSBuildOnlyFact]
        public async Task HandleMissingAssemblyFailure()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchAppMissingAssemblyFailure")
                .WithSource();

            App.Start(testAsset, [], "App");

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

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

            await App.WaitUntilOutputContains("Updated types: Printer");
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

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

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

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

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

            await App.WaitUntilOutputContains("> NewSubdir");
        }

        [PlatformSpecificFact(TestPlatforms.Windows)] // https://github.com/dotnet/aspnetcore/issues/63759
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

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

            // check that Aspire server output is logged via dotnet-watch reporter:
            await App.WaitUntilOutputContains("dotnet watch ⭐ Now listening on:");

            // wait until after all DCP sessions have started:
            await App.WaitUntilOutputContains("dotnet watch ⭐ Session started: #3");
            await App.WaitUntilOutputContains("dotnet watch ⭐ Session started: #1");
            await App.WaitUntilOutputContains("dotnet watch ⭐ Session started: #2");

            // MigrationService terminated:
            await App.WaitUntilOutputContains("dotnet watch ⭐ [#1] Sending 'sessionTerminated'");

            // working directory of the service should be it's project directory:
            await App.WaitUntilOutputContains($"ApiService working directory: '{Path.GetDirectoryName(serviceProjectPath)}'");

            // Service -- valid code change:
            UpdateSourceFile(
                serviceSourcePath,
                serviceSource.Replace("Enumerable.Range(1, 5)", "Enumerable.Range(1, 10)"));

            await App.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);

            await App.WaitUntilOutputContains("Using Aspire process launcher.");

            // Only one browser should be launched (dashboard). The child process shouldn't launch a browser.
            Assert.Equal(1, App.Process.Output.Count(line => line.StartsWith("dotnet watch ⌚ Launching browser: ")));
            App.Process.ClearOutput();

            // rude edit with build error:
            UpdateSourceFile(
                serviceSourcePath,
                serviceSource.Replace("record WeatherForecast", "record WeatherForecast2"));

            // the prompt is printed into stdout while the error is printed into stderr, so they might arrive in any order:
            await App.WaitUntilOutputContains("  ❔ Do you want to restart these projects? Yes (y) / No (n) / Always (a) / Never (v)");
            await App.WaitUntilOutputContains(MessageDescriptor.RestartNeededToApplyChanges);

            await App.WaitUntilOutputContains($"dotnet watch ❌ {serviceSourcePath}(40,1): error ENC0020: Renaming record 'WeatherForecast' requires restarting the application.");
            await App.WaitUntilOutputContains("dotnet watch ⌚ Affected projects:");
            await App.WaitUntilOutputContains("dotnet watch ⌚   WatchAspire.ApiService");
            App.Process.ClearOutput();

            App.SendKey('y');

            await App.WaitUntilOutputContains(MessageDescriptor.FixBuildError);

            await App.WaitUntilOutputContains("Application is shutting down...");

            await App.WaitUntilOutputContains($"[WatchAspire.ApiService ({tfm})] Exited");

            await App.WaitUntilOutputContains(MessageDescriptor.Building.GetMessage(serviceProjectPath));
            await App.WaitUntilOutputContains("error CS0246: The type or namespace name 'WeatherForecast' could not be found");
            App.Process.ClearOutput();

            // fix build error:
            UpdateSourceFile(
                serviceSourcePath,
                serviceSource.Replace("WeatherForecast", "WeatherForecast2"));

            await App.WaitUntilOutputContains(MessageDescriptor.ProjectsRestarted.GetMessage(1));

            await App.WaitUntilOutputContains(MessageDescriptor.BuildSucceeded.GetMessage(serviceProjectPath));
            await App.WaitUntilOutputContains(MessageDescriptor.ProjectsRebuilt);
            await App.WaitUntilOutputContains($"dotnet watch ⭐ Starting project: {serviceProjectPath}");
            App.Process.ClearOutput();

            App.SendControlC();

            await App.WaitUntilOutputContains(MessageDescriptor.ShutdownRequested);

            await App.WaitUntilOutputContains($"[WatchAspire.ApiService ({tfm})] Exited");
            await App.WaitUntilOutputContains($"[WatchAspire.Web ({tfm})] Exited");
            await App.WaitUntilOutputContains($"[WatchAspire.AppHost ({tfm})] Exited");

            await App.WaitUntilOutputContains("dotnet watch ⭐ Waiting for server to shutdown ...");

            await App.WaitUntilOutputContains("dotnet watch ⭐ Stop session #1");
            await App.WaitUntilOutputContains("dotnet watch ⭐ Stop session #2");
            await App.WaitUntilOutputContains("dotnet watch ⭐ Stop session #3");
            await App.WaitUntilOutputContains("dotnet watch ⭐ [#2] Sending 'sessionTerminated'");
            await App.WaitUntilOutputContains("dotnet watch ⭐ [#3] Sending 'sessionTerminated'");
        }

        [PlatformSpecificFact(TestPlatforms.Windows)] // https://github.com/dotnet/aspnetcore/issues/63759
        public async Task Aspire_NoEffect_AutoRestart()
        {
            var tfm = ToolsetInfo.CurrentTargetFramework;
            var testAsset = TestAssets.CopyTestAsset("WatchAspire")
                .WithSource();

            var webSourcePath = Path.Combine(testAsset.Path, "WatchAspire.Web", "Program.cs");
            var webProjectPath = Path.Combine(testAsset.Path, "WatchAspire.Web", "WatchAspire.Web.csproj");
            var webSource = File.ReadAllText(webSourcePath, Encoding.UTF8);

            App.Start(testAsset, ["-lp", "http", "--non-interactive"], relativeProjectDirectory: "WatchAspire.AppHost");

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

            await App.WaitUntilOutputContains("dotnet watch ⭐ Session started: #1");
            await App.WaitUntilOutputContains(MessageDescriptor.Exited, $"WatchAspire.MigrationService ({tfm})");
            await App.WaitUntilOutputContains("dotnet watch ⭐ [#1] Sending 'sessionTerminated'");

            // migration service output should not be printed to dotnet-watch output, it should be sent via DCP as a notification:
            await App.WaitUntilOutputContains("dotnet watch ⭐ [#1] Sending 'serviceLogs': log_message='      Migration complete', is_std_err=False");

            // wait until after DCP sessions have been started for all projects:
            await App.WaitUntilOutputContains("dotnet watch ⭐ Session started: #3");

            App.AssertOutputDoesNotContain(new Regex("^ +Migration complete"));

            App.Process.ClearOutput();

            // no-effect edit:
            UpdateSourceFile(webSourcePath, src => src.Replace("/* top-level placeholder */", "builder.Services.AddRazorComponents();"));

            await App.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);
            await App.WaitUntilOutputContains("dotnet watch ⭐ Session started: #3");
            await App.WaitUntilOutputContains(MessageDescriptor.ProjectsRestarted.GetMessage(1));
            App.AssertOutputDoesNotContain("⚠");

            // The process exited and should not participate in Hot Reload:
            App.AssertOutputDoesNotContain($"[WatchAspire.MigrationService ({tfm})]");
            App.AssertOutputDoesNotContain("dotnet watch ⭐ [#1]");

            App.Process.ClearOutput();

            // lambda body edit:
            UpdateSourceFile(webSourcePath, src => src.Replace("Hello world!", "<Updated>"));

            await App.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);
            await App.WaitUntilOutputContains($"dotnet watch 🕵️ [WatchAspire.Web ({tfm})] Updates applied.");
            App.AssertOutputDoesNotContain(MessageDescriptor.ProjectsRebuilt);
            App.AssertOutputDoesNotContain(MessageDescriptor.ProjectsRestarted);
            App.AssertOutputDoesNotContain("⚠");

            // The process exited and should not participate in Hot Reload:
            App.AssertOutputDoesNotContain($"[WatchAspire.MigrationService ({tfm})]");
            App.AssertOutputDoesNotContain("dotnet watch ⭐ [#1]");
        }
    }
}
