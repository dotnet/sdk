// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Watch.UnitTests;

public class ProjectUpdateTests(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
{
    [Fact]
    public async Task UpdateDirectoryBuildPropsThenUpdateSource()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchAppWithProjectDeps")
            .WithSource();

        var dependencyDir = Path.Combine(testAsset.Path, "Dependency");

        App.Start(testAsset, [], "AppWithDeps");

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

        UpdateSourceFile(
            Path.Combine(testAsset.Path, "Directory.Build.props"),
            src => src.Replace("<AllowUnsafeBlocks>false</AllowUnsafeBlocks>", "<AllowUnsafeBlocks>true</AllowUnsafeBlocks>"));

        await App.WaitUntilOutputContains(MessageDescriptor.NoManagedCodeChangesToApply);
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
    public async Task Update(bool isDirectoryProps)
    {
        var testAsset = TestAssets.CopyTestAsset("WatchAppWithProjectDeps", identifier: isDirectoryProps.ToString())
            .WithSource();

        var dependencyProjectDisplay = $"Dependency ({ToolsetInfo.CurrentTargetFramework})";
        var symbolName = isDirectoryProps ? "BUILD_CONST_IN_PROPS" : "BUILD_CONST_IN_CSPROJ";

        var dependencyDir = Path.Combine(testAsset.Path, "Dependency");
        var dependencySourcePath = Path.Combine(dependencyDir, "Foo.cs");
        var buildFilePath = isDirectoryProps ? Path.Combine(testAsset.Path, "Directory.Build.props") : Path.Combine(dependencyDir, "Dependency.csproj");

        File.WriteAllText(dependencySourcePath, $$"""
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
        await App.WaitUntilOutputContains($"dotnet watch ⌚ [{dependencyProjectDisplay}] [auto-restart] error ENC1102: Changing project setting 'DefineConstants'");

        await App.WaitUntilOutputContains($"{symbolName} not set");
    }

    [Fact(Skip = "https://github.com/dotnet/msbuild/issues/12001")]
    public async Task DirectoryBuildProps_Add()
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
    public async Task DirectoryBuildProps_Delete()
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
    public async Task GlobalUsings()
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
}
