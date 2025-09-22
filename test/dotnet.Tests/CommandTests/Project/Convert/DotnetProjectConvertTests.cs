// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Run.Tests;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Project.Convert.Tests;

public sealed class DotnetProjectConvertTests(ITestOutputHelper log) : SdkTest(log)
{
    /// <summary>
    /// <c>dotnet project convert</c> should result in the same project file text as <c>dotnet new console</c>.
    /// If this test fails, <c>dotnet project convert</c> command implementation should be updated.
    /// </summary>
    [Fact]
    public void SameAsTemplate()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();

        var dotnetProjectConvert = Path.Join(testInstance.Path, "dotnetProjectConvert");
        Directory.CreateDirectory(dotnetProjectConvert);

        var csFile = Path.Combine(dotnetProjectConvert, "Program.cs");
        File.WriteAllText(csFile, """Console.WriteLine("Test");""");

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(dotnetProjectConvert)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(dotnetProjectConvert)
            .EnumerateFileSystemInfos().Select(d => d.Name).Order()
            .Should().BeEquivalentTo(["Program", "Program.cs"]);

        new DirectoryInfo(Path.Join(dotnetProjectConvert, "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.csproj", "Program.cs"]);

        var dotnetProjectConvertProject = Path.Join(dotnetProjectConvert, "Program", "Program.csproj");

        Path.GetFileName(dotnetProjectConvertProject).Should().Be("Program.csproj");

        var dotnetNewConsole = Path.Join(testInstance.Path, "DotnetNewConsole");
        Directory.CreateDirectory(dotnetNewConsole);

        new DotnetCommand(Log, "new", "console")
            .WithWorkingDirectory(dotnetNewConsole)
            .Execute()
            .Should().Pass();

        var dotnetNewConsoleProject = Directory.EnumerateFiles(dotnetNewConsole, "*.csproj").Single();

        var dotnetProjectConvertProjectText = File.ReadAllText(dotnetProjectConvertProject);
        var dotnetNewConsoleProjectText = File.ReadAllText(dotnetNewConsoleProject);

        // There are some differences: we add PublishAot=true, PackAsTool=true, and UserSecretsId.
        var patchedDotnetProjectConvertProjectText = dotnetProjectConvertProjectText
            .Replace("""
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>

                """, string.Empty);
        patchedDotnetProjectConvertProjectText = Regex.Replace(patchedDotnetProjectConvertProjectText,
            """    <UserSecretsId>[^<]*<\/UserSecretsId>""" + Environment.NewLine, string.Empty);

        patchedDotnetProjectConvertProjectText.Should().Be(dotnetNewConsoleProjectText)
            .And.StartWith("""<Project Sdk="Microsoft.NET.Sdk">""");
    }

    [Theory] // https://github.com/dotnet/sdk/issues/50832
    [InlineData("File", "Lib", "../Lib", "Project", "../Lib/lib.csproj")]
    [InlineData(".", "Lib", "./Lib", "Project", "../Lib/lib.csproj")]
    [InlineData(".", "Lib", "Lib/../Lib", "Project", "../Lib/lib.csproj")]
    [InlineData("File", "Lib", "../Lib", "File/Project", "../../Lib/lib.csproj")]
    [InlineData("File", "Lib", "..\\Lib", "File/Project", "../../Lib/lib.csproj")]
    public void ProjectReference_RelativePaths(string fileDir, string libraryDir, string reference, string outputDir, string convertedReference)
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();

        var libraryDirFullPath = Path.Join(testInstance.Path, libraryDir);
        Directory.CreateDirectory(libraryDirFullPath);
        File.WriteAllText(Path.Join(libraryDirFullPath, "lib.cs"), """
            public static class C
            {
                public static void M()
                {
                    System.Console.WriteLine("Hello from library");
                }
            }
            """);
        File.WriteAllText(Path.Join(libraryDirFullPath, "lib.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var fileDirFullPath = Path.Join(testInstance.Path, fileDir);
        Directory.CreateDirectory(fileDirFullPath);
        File.WriteAllText(Path.Join(fileDirFullPath, "app.cs"), $"""
            #:project {reference}
            C.M();
            """);

        var expectedOutput = "Hello from library";

        new DotnetCommand(Log, "run", "app.cs")
            .WithWorkingDirectory(fileDirFullPath)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        var outputDirFullPath = Path.Join(testInstance.Path, outputDir);
        new DotnetCommand(Log, "project", "convert", "app.cs", "-o", outputDirFullPath)
            .WithWorkingDirectory(fileDirFullPath)
            .Execute()
            .Should().Pass();

        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(outputDirFullPath)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        File.ReadAllText(Path.Join(outputDirFullPath, "app.csproj"))
            .Should().Contain($"""
                <ProjectReference Include="{convertedReference.Replace('/', Path.DirectorySeparatorChar)}" />
                """);
    }

    [Fact] // https://github.com/dotnet/sdk/issues/50832
    public void ProjectReference_FullPath()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();

        var libraryDirFullPath = Path.Join(testInstance.Path, "Lib");
        Directory.CreateDirectory(libraryDirFullPath);
        File.WriteAllText(Path.Join(libraryDirFullPath, "lib.cs"), """
            public static class C
            {
                public static void M()
                {
                    System.Console.WriteLine("Hello from library");
                }
            }
            """);
        File.WriteAllText(Path.Join(libraryDirFullPath, "lib.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var fileDirFullPath = Path.Join(testInstance.Path, "File");
        Directory.CreateDirectory(fileDirFullPath);
        File.WriteAllText(Path.Join(fileDirFullPath, "app.cs"), $"""
            #:project {libraryDirFullPath}
            C.M();
            """);

        var expectedOutput = "Hello from library";

        new DotnetCommand(Log, "run", "app.cs")
            .WithWorkingDirectory(fileDirFullPath)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        var outputDirFullPath = Path.Join(testInstance.Path, "File/Project");
        new DotnetCommand(Log, "project", "convert", "app.cs", "-o", outputDirFullPath)
            .WithWorkingDirectory(fileDirFullPath)
            .Execute()
            .Should().Pass();

        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(outputDirFullPath)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        File.ReadAllText(Path.Join(outputDirFullPath, "app.csproj"))
            .Should().Contain($"""
                <ProjectReference Include="{Path.Join(libraryDirFullPath, "lib.csproj")}" />
                """);
    }

    [Fact]
    public void DirectoryAlreadyExists()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var directoryPath = Path.Join(testInstance.Path, "MyApp");
        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(Path.Join(testInstance.Path, "MyApp.cs"), "Console.WriteLine();");

        new DotnetCommand(Log, "project", "convert", "MyApp.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.DirectoryAlreadyExists, directoryPath));

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(d => d.Name).Order()
            .Should().BeEquivalentTo(["MyApp", "MyApp.cs"]);
    }

    [Fact]
    public void OutputOption()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        Directory.CreateDirectory(Path.Join(testInstance.Path, "MyApp"));
        File.WriteAllText(Path.Join(testInstance.Path, "MyApp.cs"), "Console.WriteLine();");

        new DotnetCommand(Log, "project", "convert", "MyApp.cs", "-o", "MyApp1")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateDirectories().Select(d => d.Name).Order()
            .Should().BeEquivalentTo(["MyApp", "MyApp1"]);

        new DirectoryInfo(Path.Join(testInstance.Path, "MyApp"))
            .EnumerateFileSystemInfos().Should().BeEmpty();

        new DirectoryInfo(Path.Join(testInstance.Path, "MyApp1"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["MyApp.csproj", "MyApp.cs"]);
    }

    [Fact]
    public void OutputOption_DirectoryAlreadyExists()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var directoryPath = Path.Join(testInstance.Path, "SomeOutput");
        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(Path.Join(testInstance.Path, "MyApp.cs"), "Console.WriteLine();");

        new DotnetCommand(Log, "project", "convert", "MyApp.cs", "-o", "SomeOutput")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.DirectoryAlreadyExists, directoryPath));

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(d => d.Name).Order()
            .Should().BeEquivalentTo(["MyApp.cs", "SomeOutput"]);
    }

    [Fact]
    public void MultipleEntryPointFiles()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program1.cs"), "Console.WriteLine(1);");
        File.WriteAllText(Path.Join(testInstance.Path, "Program2.cs"), "Console.WriteLine(2);");

        new DotnetCommand(Log, "project", "convert", "Program1.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(d => d.Name).Order()
            .Should().BeEquivalentTo(["Program1", "Program1.cs", "Program2.cs"]);

        new DirectoryInfo(Path.Join(testInstance.Path, "Program1"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program1.csproj", "Program1.cs"]);
    }

    [Fact]
    public void NoFileArgument()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();

        new DotnetCommand(Log, "project", "convert")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining("convert"); // Required argument missing for command 'convert'

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Should().BeEmpty();
    }

    [Fact]
    public void NonExistentFile()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();

        new DotnetCommand(Log, "project", "convert", "NotHere.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.InvalidFilePath, Path.Join(testInstance.Path, "NotHere.cs")));

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Should().BeEmpty();
    }

    [Fact]
    public void NonCSharpFile()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var filePath = Path.Join(testInstance.Path, "Program.vb");
        File.WriteAllText(filePath, "");

        new DotnetCommand(Log, "project", "convert", "Program.vb")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.InvalidFilePath, filePath));

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.vb"]);
    }

    [Fact]
    public void ExtensionCasing()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.CS"), "Console.WriteLine();");

        new DotnetCommand(Log, "project", "convert", "Program.CS")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program", "Program.CS"]);

        new DirectoryInfo(Path.Join(testInstance.Path, "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.csproj", "Program.CS"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("class C;")]
    public void FileContent(string content)
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), content);

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program", "Program.cs"]);

        File.ReadAllText(Path.Join(testInstance.Path, "Program.cs"))
            .Should().Be(content);

        new DirectoryInfo(Path.Join(testInstance.Path, "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.csproj", "Program.cs"]);

        File.ReadAllText(Path.Join(testInstance.Path, "Program", "Program.cs"))
            .Should().Be(content);
    }

    [Fact]
    public void NestedDirectory()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var appDirectory = Path.Join(testInstance.Path, "app");
        Directory.CreateDirectory(appDirectory);
        File.WriteAllText(Path.Join(appDirectory, "Program.cs"), "Console.WriteLine();");

        new DotnetCommand(Log, "project", "convert", "app/Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(Path.Join(testInstance.Path, "app"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program", "Program.cs"]);

        new DirectoryInfo(Path.Join(testInstance.Path, "app", "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.csproj", "Program.cs"]);
    }

    /// <summary>
    /// Default items like <c>None</c> or <c>Content</c> are copied over.
    /// </summary>
    [Fact]
    public void DefaultItems()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            Console.WriteLine();
            """);
        File.WriteAllText(Path.Join(testInstance.Path, "my.json"), "");
        File.WriteAllText(Path.Join(testInstance.Path, "Resources.resx"), "");
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), "");
        Directory.CreateDirectory(Path.Join(testInstance.Path, "subdir"));
        File.WriteAllText(Path.Join(testInstance.Path, "subdir", "second.json"), "");

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program", "Program.cs", "Resources.resx", "Util.cs", "my.json", "subdir"]);

        new DirectoryInfo(Path.Join(testInstance.Path, "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.csproj", "Resources.resx", "Program.cs", "my.json", "subdir"]);

        new DirectoryInfo(Path.Join(testInstance.Path, "Program", "subdir"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["second.json"]);
    }

    [Fact]
    public void DefaultItems_MoreIncluded()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:property EnableDefaultCompileItems=true
            Console.WriteLine();
            """);
        File.WriteAllText(Path.Join(testInstance.Path, "my.json"), "");
        File.WriteAllText(Path.Join(testInstance.Path, "Resources.resx"), "");
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), "");

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program", "Program.cs", "Resources.resx", "Util.cs", "my.json"]);

        new DirectoryInfo(Path.Join(testInstance.Path, "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.csproj", "Program.cs", "Resources.resx", "Util.cs", "my.json"]);
    }

    [Fact]
    public void DefaultItems_MoreExcluded()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:property EnableDefaultItems=false
            Console.WriteLine();
            """);
        File.WriteAllText(Path.Join(testInstance.Path, "my.json"), "");
        File.WriteAllText(Path.Join(testInstance.Path, "Resources.resx"), "");
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), "");

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program", "Program.cs", "Resources.resx", "Util.cs", "my.json"]);

        new DirectoryInfo(Path.Join(testInstance.Path, "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.csproj", "Program.cs"]);
    }

    /// <summary>
    /// <c>ExcludeFromFileBasedAppConversion</c> metadata can be used to exclude items from the conversion.
    /// </summary>
    [Fact]
    public void DefaultItems_ExcludedViaMetadata()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            Console.WriteLine();
            """);
        File.WriteAllText(Path.Join(testInstance.Path, "my.json"), "");
        File.WriteAllText(Path.Join(testInstance.Path, "Resources.resx"), "");
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), "");
        File.WriteAllText(Path.Join(testInstance.Path, "second.json"), "");

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.targets"), """
            <Project>
                <ItemGroup>
                    <None Update="$(MSBuildThisFileDirectory)second.json" ExcludeFromFileBasedAppConversion="true" />
                </ItemGroup>
            </Project>
            """);

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Directory.Build.targets", "Program", "Program.cs", "Resources.resx", "Util.cs", "my.json", "second.json"]);

        // `second.json` is excluded from the conversion.
        new DirectoryInfo(Path.Join(testInstance.Path, "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Directory.Build.targets", "Program.csproj", "Resources.resx", "Program.cs", "my.json"]);
    }

    [Fact]
    public void DefaultItems_ImplicitBuildFileInDirectory()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            Console.WriteLine(Util.GetText());
            """);
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), """
            class Util { public static string GetText() => "Hi from Util"; }
            """);
        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
                <ItemGroup>
                    <Compile Include="Util.cs" />
                </ItemGroup>
            </Project>
            """);

        // The app works before conversion.
        string expectedOutput = "Hi from Util";
        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        // Convert.
        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Directory.Build.props", "Program", "Program.cs", "Util.cs"]);

        // Directory.Build.props is included as it's a None item.
        new DirectoryInfo(Path.Join(testInstance.Path, "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Directory.Build.props", "Program.csproj", "Program.cs", "Util.cs"]);

        // The app works after conversion.
        new DotnetCommand(Log, "run", "Program/Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);
    }

    [Fact]
    public void DefaultItems_ImplicitBuildFileOutsideDirectory()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var subdir = Path.Join(testInstance.Path, "subdir");
        Directory.CreateDirectory(subdir);
        File.WriteAllText(Path.Join(subdir, "Program.cs"), """
            Console.WriteLine(Util.GetText());
            """);
        File.WriteAllText(Path.Join(subdir, "Util.cs"), """
            class Util { public static string GetText() => "Hi from Util"; }
            """);
        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
                <ItemGroup>
                    <Compile Include="$(MSBuildThisFileDirectory)subdir\Util.cs" />
                </ItemGroup>
            </Project>
            """);

        // The app works before conversion.
        string expectedOutput = "Hi from Util";
        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(subdir)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        // Convert.
        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(subdir)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(subdir)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program", "Program.cs", "Util.cs"]);

        new DirectoryInfo(Path.Join(subdir, "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.csproj", "Program.cs", "Util.cs"]);

        // The app works after conversion.
        new DotnetCommand(Log, "run", "Program/Program.cs")
            .WithWorkingDirectory(subdir)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);
    }

    [Fact]
    public void DefaultItems_ImplicitBuildFileAndUtilOutsideDirectory()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var subdir = Path.Join(testInstance.Path, "subdir");
        Directory.CreateDirectory(subdir);
        File.WriteAllText(Path.Join(subdir, "Program.cs"), """
            Console.WriteLine(Util.GetText());
            """);
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), """
            class Util { public static string GetText() => "Hi from Util"; }
            """);
        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
                <ItemGroup>
                    <Compile Include="$(MSBuildThisFileDirectory)Util.cs" />
                </ItemGroup>
            </Project>
            """);

        // The app works before conversion.
        string expectedOutput = "Hi from Util";
        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(subdir)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        // Convert.
        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(subdir)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(subdir)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program", "Program.cs"]);

        new DirectoryInfo(Path.Join(subdir, "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.csproj", "Program.cs"]);

        // The app works after conversion.
        new DotnetCommand(Log, "run", "Program/Program.cs")
            .WithWorkingDirectory(subdir)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);
    }

    /// <summary>
    /// Scripts in repo root should not include default items.
    /// Part of <see href="https://github.com/dotnet/sdk/issues/49826"/>.
    /// </summary>
    [Theory, CombinatorialData]
    public void DefaultItems_AlongsideProj([CombinatorialValues("sln", "slnx", "csproj", "vbproj", "shproj", "proj")] string ext)
    {
        bool considered = ext is "sln" or "slnx" or "csproj";

        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            Console.WriteLine();
            """);
        File.WriteAllText(Path.Join(testInstance.Path, "my.json"), "");
        File.WriteAllText(Path.Join(testInstance.Path, "Resources.resx"), "");
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), "");
        File.WriteAllText(Path.Join(testInstance.Path, $"repo.{ext}"), "");

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program", "Program.cs", "Resources.resx", "Util.cs", "my.json", $"repo.{ext}"]);

        new DirectoryInfo(Path.Join(testInstance.Path, "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(considered
                ? ["Program.csproj", "Program.cs"]
                : ["my.json", "Program.csproj", "Program.cs", "Resources.resx"]);
    }

    /// <summary>
    /// When processing fails due to invalid directives, no conversion should be performed
    /// (e.g., the target directory should not be created).
    /// </summary>
    [Fact]
    public void ProcessingFails()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var filePath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(filePath, "#:invalid");

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(RunFileTests.DirectiveError(filePath, 1, CliCommandStrings.UnrecognizedDirective, "invalid"));

        new DirectoryInfo(Path.Join(testInstance.Path))
            .EnumerateDirectories().Should().BeEmpty();
    }

    /// <summary>
    /// Since we perform MSBuild evaluation during the conversion (to find included items to copy over),
    /// the conversion can fail when the specified SDK does not exist.
    /// </summary>
    [Fact]
    public void ProcessingFails_Evaluation()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var filePath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(filePath, "#:sdk Microsoft.ThisSdkDoesNotExist");

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            // The SDK 'Microsoft.ThisSdkDoesNotExist' specified could not be found.
            .And.HaveStdErrContaining("Microsoft.ThisSdkDoesNotExist");

        new DirectoryInfo(Path.Join(testInstance.Path))
            .EnumerateDirectories().Should().BeEmpty();
    }

    /// <summary>
    /// End-to-end test of directive processing. More cases are covered by faster unit tests below.
    /// </summary>
    [Fact]
    public void ProcessingSucceeds()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var originalSource = """
            #:package Humanizer@2.14.1
            Console.WriteLine();
            """;
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), originalSource);

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program", "Program.cs"]);

        File.ReadAllText(Path.Join(testInstance.Path, "Program.cs"))
            .Should().Be(originalSource);

        new DirectoryInfo(Path.Join(testInstance.Path, "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.csproj", "Program.cs"]);

        File.ReadAllText(Path.Join(testInstance.Path, "Program", "Program.cs"))
            .Should().Be("Console.WriteLine();");

        File.ReadAllText(Path.Join(testInstance.Path, "Program", "Program.csproj"))
            .Should().Match($"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <UserSecretsId>Program-*</UserSecretsId>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Humanizer" Version="2.14.1" />
                  </ItemGroup>

                </Project>

                """);
    }

    [Theory, CombinatorialData]
    public void UserSecretsId_Overridden_ViaDirective(bool hasDirectiveBuildProps)
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:property UserSecretsId=MyIdFromDirective
            Console.WriteLine();
            """);

        if (hasDirectiveBuildProps)
        {
            File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
                <Project>
                  <PropertyGroup>
                    <UserSecretsId>MyIdFromDirBuildProps</UserSecretsId>
                  </PropertyGroup>
                </Project>
                """);
        }

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        File.ReadAllText(Path.Join(testInstance.Path, "Program", "Program.csproj"))
            .Should().Be($"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <UserSecretsId>MyIdFromDirective</UserSecretsId>
                  </PropertyGroup>

                </Project>

                """);
    }

    [Fact]
    public void UserSecretsId_Overridden_ViaDirectoryBuildProps()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            Console.WriteLine();
            """);
        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <UserSecretsId>MyIdFromDirBuildProps</UserSecretsId>
              </PropertyGroup>
            </Project>
            """);

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        File.ReadAllText(Path.Join(testInstance.Path, "Program", "Program.csproj"))
            .Should().Be($"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                  </PropertyGroup>

                </Project>

                """);
    }

    [Theory, CombinatorialData]
    public void UserSecretsId_Overridden_SameAsImplicit(bool hasDirective, bool hasDirectiveBuildProps)
    {
        const string implicitValue = "$(MSBuildProjectName)-$([MSBuild]::StableStringHash($(MSBuildProjectFullPath.ToLowerInvariant()), 'Sha256'))";

        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), $"""
            {(hasDirective ? $"#:property UserSecretsId={implicitValue}" : "")}
            Console.WriteLine();
            """);

        if (hasDirectiveBuildProps)
        {
            File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), $"""
                <Project>
                  <PropertyGroup>
                    <UserSecretsId>{SecurityElement.Escape(implicitValue)}</UserSecretsId>
                  </PropertyGroup>
                </Project>
                """);
        }

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        File.ReadAllText(Path.Join(testInstance.Path, "Program", "Program.csproj"))
            .Should().Match($"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <UserSecretsId>{(hasDirective ? SecurityElement.Escape(implicitValue) : "Program-*")}</UserSecretsId>
                  </PropertyGroup>

                </Project>

                """);
    }

    [Fact]
    public void Directives()
    {
        VerifyConversion(
            inputCSharp: """
                #!/program
                #:sdk Microsoft.NET.Sdk
                #:sdk Aspire.Hosting.Sdk@9.1.0
                #:property TargetFramework=net472
                #:package System.CommandLine@2.0.0-beta4.22272.1
                #:property LangVersion=preview
                Console.WriteLine();
                """,
            expectedProject: """
                <Project Sdk="Microsoft.NET.Sdk">

                  <Sdk Name="Aspire.Hosting.Sdk" Version="9.1.0" />

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <TargetFramework>net472</TargetFramework>
                    <LangVersion>preview</LangVersion>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: """
                Console.WriteLine();
                """);
    }

    /// <summary>
    /// There should be only one <c>PropertyGroup</c> element when the default properties are overridden.
    /// </summary>
    [Fact]
    public void Directives_AllDefaultOverridden()
    {
        VerifyConversion(
            inputCSharp: """
                #!/program
                #:sdk Microsoft.NET.Web.Sdk
                #:property OutputType=Exe
                #:property TargetFramework=net472
                #:property Nullable=disable
                #:property PublishAot=false
                #:property PackAsTool=false
                #:property Custom=1
                #:property ImplicitUsings=disable
                Console.WriteLine();
                """,
            expectedProject: """
                <Project Sdk="Microsoft.NET.Web.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net472</TargetFramework>
                    <Nullable>disable</Nullable>
                    <PublishAot>false</PublishAot>
                    <PackAsTool>false</PackAsTool>
                    <Custom>1</Custom>
                    <ImplicitUsings>disable</ImplicitUsings>
                  </PropertyGroup>

                </Project>

                """,
            expectedCSharp: """
                Console.WriteLine();
                """);
    }

    [Fact]
    public void Directives_Variable()
    {
        VerifyConversion(
            inputCSharp: """
                #:package MyPackage@$(MyProp)
                #:property MyProp=MyValue
                """,
            expectedProject: $"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <MyProp>MyValue</MyProp>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="MyPackage" Version="$(MyProp)" />
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: "");
    }

    [Fact]
    public void Directives_DirectoryPath()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();

        var libDir = Path.Join(testInstance.Path, "lib");
        Directory.CreateDirectory(libDir);
        File.WriteAllText(Path.Join(libDir, "Lib.csproj"), "test");

        var slash = Path.DirectorySeparatorChar;
        VerifyConversion(
            filePath: Path.Join(testInstance.Path, "app", "Program.cs"),
            inputCSharp: """
                #:project ../lib
                """,
            expectedProject: $"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                  </PropertyGroup>

                  <ItemGroup>
                    <ProjectReference Include="..{slash}lib{slash}Lib.csproj" />
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: "");
    }

    [Fact]
    public void Directives_Separators()
    {
        VerifyConversion(
            inputCSharp: """
                #:property Prop1 = One=a/b
                #:property Prop2 = Two/a=b
                #:sdk First @ 1.0=a/b
                #:sdk Second @ 2.0/a=b
                #:sdk Third @ 3.0=a/b
                #:package P1 @ 1.0/a=b
                #:package P2 @ 2.0/a=b
                #:package P3@1.0 ab
                """,
            expectedProject: $"""
                <Project Sdk="First/1.0=a/b">

                  <Sdk Name="Second" Version="2.0/a=b" />
                  <Sdk Name="Third" Version="3.0=a/b" />

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <Prop1>One=a/b</Prop1>
                    <Prop2>Two/a=b</Prop2>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="P1" Version="1.0/a=b" />
                    <PackageReference Include="P2" Version="2.0/a=b" />
                    <PackageReference Include="P3" Version="1.0 ab" />
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: "");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("SDK")]
    public void Directives_Unknown(string directive)
    {
        VerifyConversionThrows(
            inputCSharp: $"""
                #:sdk Test
                #:{directive} Test
                """,
            expectedWildcardPattern: RunFileTests.DirectiveError("/app/Program.cs", 2, CliCommandStrings.UnrecognizedDirective, directive));
    }

    [Fact]
    public void Directives_Empty()
    {
        VerifyConversionThrows(
            inputCSharp: """
                #:
                #:sdk Test
                """,
            expectedWildcardPattern: RunFileTests.DirectiveError("/app/Program.cs", 1, CliCommandStrings.UnrecognizedDirective, ""));
    }

    [Theory, CombinatorialData]
    public void Directives_EmptyName(
        [CombinatorialValues("sdk", "property", "package", "project")] string directive,
        [CombinatorialValues(" ", "")] string value)
    {
        VerifyConversionThrows(
            inputCSharp: $"""
                #:{directive}{value}
                """,
            expectedWildcardPattern: RunFileTests.DirectiveError("/app/Program.cs", 1, CliCommandStrings.MissingDirectiveName, directive));
    }

    [Fact]
    public void Directives_MissingPropertyValue()
    {
        VerifyConversionThrows(
            inputCSharp: """
                #:property Test
                """,
            expectedWildcardPattern: RunFileTests.DirectiveError("/app/Program.cs", 1, CliCommandStrings.PropertyDirectiveMissingParts));
    }

    [Fact]
    public void Directives_InvalidPropertyName()
    {
        VerifyConversionThrows(
            inputCSharp: """
                #:property Name"=Value
                """,
            expectedWildcardPattern: RunFileTests.DirectiveError("/app/Program.cs", 1, CliCommandStrings.PropertyDirectiveInvalidName, """
                The '"' character, hexadecimal value 0x22, cannot be included in a name.
                """));
    }

    [Theory]
    [InlineData("sdk", "@", "/")]
    [InlineData("sdk", "@", " ")]
    [InlineData("sdk", "@", "=")]
    [InlineData("package", "@", "/")]
    [InlineData("package", "@", " ")]
    [InlineData("package", "@", "=")]
    [InlineData("property", "=", "/")]
    [InlineData("property", "=", " ")]
    [InlineData("property", "=", "@")]
    public void Directives_InvalidName(string directiveKind, string expectedSeparator, string actualSeparator)
    {
        VerifyConversionThrows(
            inputCSharp: $"#:{directiveKind} Abc{actualSeparator}Xyz",
            expectedWildcardPattern: RunFileTests.DirectiveError("/app/Program.cs", 1, CliCommandStrings.InvalidDirectiveName, directiveKind, expectedSeparator));
    }

    [Fact]
    public void Directives_Escaping()
    {
        VerifyConversion(
            inputCSharp: """
                #:property Prop=<test">
                #:sdk <test"> @="<>test
                #:package <test"> @="<>test
                """,
            expectedProject: $"""
                <Project Sdk="&lt;test&quot;&gt;/=&quot;&lt;&gt;test">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <Prop>&lt;test&quot;&gt;</Prop>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="&lt;test&quot;&gt;" Version="=&quot;&lt;&gt;test" />
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: "");
    }

    [Fact]
    public void Directives_Whitespace()
    {
        VerifyConversion(
            inputCSharp: """
                    #:   sdk   TestSdk
                #:property Name  =  Value   
                #:property NugetPackageDescription="My package with spaces"
                 #  !  /test
                  #!  /program   x   
                 # :property Name=Value
                """,
            expectedProject: $"""
                <Project Sdk="TestSdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <Name>Value</Name>
                    <NugetPackageDescription>&quot;My package with spaces&quot;</NugetPackageDescription>
                  </PropertyGroup>

                </Project>

                """,
            expectedCSharp: """
                 #  !  /test
                  #!  /program   x   
                 # :property Name=Value
                """);
    }

    [Fact]
    public void Directives_BlankLines()
    {
        var expectedProject = $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <PublishAot>true</PublishAot>
                <PackAsTool>true</PackAsTool>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="A" Version="B" />
              </ItemGroup>

            </Project>

            """;

        VerifyConversion(
            inputCSharp: """
                #:package A@B

                Console.WriteLine();
                """,
            expectedProject: expectedProject,
            expectedCSharp: """

                Console.WriteLine();
                """);

        VerifyConversion(
            inputCSharp: """

                #:package A@B
                Console.WriteLine();
                """,
            expectedProject: expectedProject,
            expectedCSharp: """

                Console.WriteLine();
                """);
    }

    /// <summary>
    /// <c>#:</c> directives after C# code are ignored.
    /// </summary>
    [Fact]
    public void Directives_AfterToken()
    {
        string source = """
            #:property Prop1=1
            #define X
            #:property Prop2=2
            Console.WriteLine();
            #:property Prop1=3
            """;

        VerifyConversionThrows(
            inputCSharp: source,
            expectedWildcardPattern: RunFileTests.DirectiveError("/app/Program.cs", 5, CliCommandStrings.CannotConvertDirective));

        VerifyConversion(
            inputCSharp: source,
            force: true,
            expectedProject: $"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <Prop1>1</Prop1>
                    <Prop2>2</Prop2>
                  </PropertyGroup>

                </Project>

                """,
            expectedCSharp: """
                #define X
                Console.WriteLine();
                #:property Prop1=3
                """);
    }

    /// <summary>
    /// <c>#:</c> directives after <c>#if</c> are ignored.
    /// </summary>
    [Fact]
    public void Directives_AfterIf()
    {
        string source = """
            #:property Prop1=1
            #define X
            #:property Prop2=2
            #if X
            #:property Prop1=3
            #endif
            #:property Prop2=4
            """;

        VerifyConversionThrows(
            inputCSharp: source,
            expectedWildcardPattern: RunFileTests.DirectiveError("/app/Program.cs", 5, CliCommandStrings.CannotConvertDirective));

        VerifyConversion(
            inputCSharp: source,
            force: true,
            expectedProject: $"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <Prop1>1</Prop1>
                    <Prop2>2</Prop2>
                  </PropertyGroup>

                </Project>

                """,
            expectedCSharp: """
                #define X
                #if X
                #:property Prop1=3
                #endif
                #:property Prop2=4
                """);
    }

    /// <summary>
    /// Comments are not currently converted.
    /// </summary>
    [Fact]
    public void Directives_Comments()
    {
        VerifyConversion(
            inputCSharp: """
                // License for this file
                #:sdk MySdk
                // This package is needed for Json
                #:package MyJson
                // #:package Unused
                /* Custom props: */
                #:property Prop1=1
                #:property Prop2=2
                Console.Write();
                """,
            expectedProject: $"""
                <Project Sdk="MySdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <Prop1>1</Prop1>
                    <Prop2>2</Prop2>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="MyJson" />
                  </ItemGroup>

                </Project>

                """,
            expectedCSharp: """
                // License for this file
                // This package is needed for Json
                // #:package Unused
                /* Custom props: */
                Console.Write();
                """);
    }

    [Fact]
    public void Directives_Duplicate()
    {
        VerifyDirectiveConversionErrors(
            inputCSharp: """
                #:property Prop=1
                #:property Prop=2
                """,
            expectedErrors:
            [
                (2, string.Format(CliCommandStrings.DuplicateDirective, "#:property Prop")),
            ]);

        VerifyDirectiveConversionErrors(
            inputCSharp: """
                #:sdk Name
                #:sdk Name@X
                #:sdk Name
                #:sdk Name2
                """,
            expectedErrors:
            [
                (2, string.Format(CliCommandStrings.DuplicateDirective, "#:sdk Name")),
                (3, string.Format(CliCommandStrings.DuplicateDirective, "#:sdk Name")),
            ]);

        VerifyDirectiveConversionErrors(
            inputCSharp: """
                #:package Name
                #:package Name@X
                #:package Name
                #:package Name2
                """,
            expectedErrors:
            [
                (2, string.Format(CliCommandStrings.DuplicateDirective, "#:package Name")),
                (3, string.Format(CliCommandStrings.DuplicateDirective, "#:package Name")),
            ]);

        VerifyDirectiveConversionErrors(
            inputCSharp: """
                #:sdk Prop@1
                #:property Prop=2
                """,
            expectedErrors: []);

        VerifyDirectiveConversionErrors(
            inputCSharp: """
                #:property Prop=1
                #:property Prop=2
                #:property Prop2=3
                #:property Prop=4
                """,
            expectedErrors:
            [
                (2, string.Format(CliCommandStrings.DuplicateDirective, "#:property Prop")),
                (4, string.Format(CliCommandStrings.DuplicateDirective, "#:property Prop")),
            ]);

        VerifyDirectiveConversionErrors(
            inputCSharp: """
                #:property prop=1
                #:property PROP=2
                """,
            expectedErrors:
            [
                (2, string.Format(CliCommandStrings.DuplicateDirective, "#:property prop")),
            ]);
    }

    [Fact] // https://github.com/dotnet/sdk/issues/49797
    public void Directives_VersionedSdkFirst()
    {
        VerifyConversion(
            inputCSharp: """
                #:sdk Microsoft.NET.Sdk@9.0.0
                Console.WriteLine();
                """,
            expectedProject: $"""
                <Project Sdk="Microsoft.NET.Sdk/9.0.0">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                  </PropertyGroup>

                </Project>

                """,
            expectedCSharp: """
                Console.WriteLine();
                """);
    }

    private static void Convert(string inputCSharp, out string actualProject, out string? actualCSharp, bool force, string? filePath)
    {
        var sourceFile = new SourceFile(filePath ?? "/app/Program.cs", SourceText.From(inputCSharp, Encoding.UTF8));
        var directives = VirtualProjectBuildingCommand.FindDirectives(sourceFile, reportAllErrors: !force, DiagnosticBag.ThrowOnFirst());
        var projectWriter = new StringWriter();
        VirtualProjectBuildingCommand.WriteProjectFile(projectWriter, directives, isVirtualProject: false);
        actualProject = projectWriter.ToString();
        actualCSharp = VirtualProjectBuildingCommand.RemoveDirectivesFromFile(directives, sourceFile.Text)?.ToString();
    }

    /// <param name="expectedCSharp">
    /// <see langword="null"/> means the conversion should not touch the C# content.
    /// </param>
    private static void VerifyConversion(string inputCSharp, string expectedProject, string? expectedCSharp, bool force = false, string? filePath = null)
    {
        Convert(inputCSharp, out var actualProject, out var actualCSharp, force: force, filePath: filePath);
        actualProject.Should().Be(expectedProject);
        actualCSharp.Should().Be(expectedCSharp);
    }

    private static void VerifyConversionThrows(string inputCSharp, string expectedWildcardPattern)
    {
        var convert = () => Convert(inputCSharp, out _, out _, force: false, filePath: null);
        convert.Should().Throw<GracefulException>().WithMessage(expectedWildcardPattern);
    }

    private static void VerifyDirectiveConversionErrors(string inputCSharp, IEnumerable<(int LineNumber, string Message)> expectedErrors)
    {
        var programPath = "/app/Program.cs";
        var sourceFile = new SourceFile(programPath, SourceText.From(inputCSharp, Encoding.UTF8));
        VirtualProjectBuildingCommand.FindDirectives(sourceFile, reportAllErrors: true, DiagnosticBag.Collect(out var diagnostics));
        Assert.All(diagnostics, d => { Assert.Equal(programPath, d.Location.Path); });
        diagnostics.Select(d => (d.Location.Span.Start.Line + 1, d.Message)).Should().BeEquivalentTo(expectedErrors);
    }
}
