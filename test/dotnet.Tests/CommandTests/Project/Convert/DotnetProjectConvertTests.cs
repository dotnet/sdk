// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Run;
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

        new DirectoryInfo(dotnetProjectConvert).Should().HaveSubtree("""
            Program.cs
            Program.csproj
            """);

        var dotnetProjectConvertProject = Path.Join(dotnetProjectConvert, "Program.csproj");

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
        dotnetProjectConvertProjectText.Should().Be(dotnetNewConsoleProjectText)
            .And.StartWith("""<Project Sdk="Microsoft.NET.Sdk">""");
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

        new DirectoryInfo(testInstance.Path).Should().HaveSubtree("""
            MyApp1/
            MyApp1/MyApp.cs
            MyApp1/MyApp.csproj
            MyApp1/MyApp/
            """);
    }

    [Fact]
    public void OutputOption_SharedConflictDoesNotMatter()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        Directory.CreateDirectory(Path.Join(testInstance.Path, "MyApp"));
        File.WriteAllText(Path.Join(testInstance.Path, "Shared.cs"), "Console.WriteLine();");

        new DotnetCommand(Log, "project", "convert", "Shared.cs", "-o", "MyApp1")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path).Should().HaveSubtree("""
            MyApp1/
            MyApp1/MyApp/
            MyApp1/Shared.cs
            MyApp1/Shared.csproj
            """);
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

        new DirectoryInfo(testInstance.Path).Should().HaveSubtree("""
            MyApp.cs
            SomeOutput/
            """);
    }

    [Fact]
    public void OutputOption_SameAsSource()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "MyApp.cs"), "Console.WriteLine();");

        new DotnetCommand(Log, "project", "convert", "MyApp.cs", "-o", ".")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.DirectoryAlreadyExists, testInstance.Path));
    }

    [Fact]
    public void OutputOption_SameAsNestedSource()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        Directory.CreateDirectory(Path.Join(testInstance.Path, "app"));
        File.WriteAllText(Path.Join(testInstance.Path, "app", "MyApp.cs"), "Console.WriteLine();");

        new DotnetCommand(Log, "project", "convert", "app/MyApp.cs", "-o", "app")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.DirectoryAlreadyExists, Path.Join(testInstance.Path, "app")));
    }

    [Fact]
    public void OutputOption_SameAsParentCurrent()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        Directory.CreateDirectory(Path.Join(testInstance.Path, "app"));
        File.WriteAllText(Path.Join(testInstance.Path, "app", "MyApp.cs"), "Console.WriteLine();");

        new DotnetCommand(Log, "project", "convert", "app/MyApp.cs", "-o", ".")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.DirectoryAlreadyExists, testInstance.Path));
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

        new DirectoryInfo(testInstance.Path).Should().HaveSubtree(string.Empty);
    }

    [Fact]
    public void NonExistentFile()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();

        new DotnetCommand(Log, "project", "convert", "NotHere.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.InvalidFileOrDirectoryPath, Path.Join(testInstance.Path, "NotHere.cs")));

        new DirectoryInfo(testInstance.Path).Should().HaveSubtree(string.Empty);
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
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.InvalidFileOrDirectoryPath, filePath));

        new DirectoryInfo(testInstance.Path).Should().HaveSubtree("""
            Program.vb
            """);
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

        new DirectoryInfo(testInstance.Path).Should().HaveSubtree("""
            Program.CS
            Program.csproj
            """);
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
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.NoTopLevelStatements, Path.Join(testInstance.Path, "Program.cs")));

        new DirectoryInfo(testInstance.Path).Should().HaveSubtree("""
            Program.cs
            """);

        File.ReadAllText(Path.Join(testInstance.Path, "Program.cs"))
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

        new DirectoryInfo(testInstance.Path).Should().HaveSubtree("""
            app/
            app/Program.cs
            app/Program.csproj
            """);
    }

    [Fact]
    public void NestedEntryPoint_Single()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        Directory.CreateDirectory(Path.Join(testInstance.Path, "app"));
        File.WriteAllText(Path.Join(testInstance.Path, "app", "Program.cs"), "Console.WriteLine();");

        new DotnetCommand(Log, "project", "convert", ".")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.EntryPointInNestedFolder, Path.Join(testInstance.Path, "app", "Program.cs")));
    }

    [Theory]
    [InlineData("Program.cs")]
    [InlineData(".")]
    public void NestedEntryPoint_Another(string arg)
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), "Console.Write(1);");
        Directory.CreateDirectory(Path.Join(testInstance.Path, "app"));
        File.WriteAllText(Path.Join(testInstance.Path, "app", "Program.cs"), "Console.Write(2);");

        new DotnetCommand(Log, "project", "convert", arg)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.EntryPointInNestedFolder, Path.Join(testInstance.Path, "app", "Program.cs")));
    }

    [Fact]
    public void NoEntryPoints()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), "class C;");

        new DotnetCommand(Log, "project", "convert", ".")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.NoEntryPoints, testInstance.Path));
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
        File.WriteAllText(filePath, """
            #:invalid
            Console.WriteLine();
            """);

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.UnrecognizedDirective, "invalid", $"{filePath}:1"));

        new DirectoryInfo(testInstance.Path).Should().HaveSubtree("""
            Program.cs
            """);
    }

    /// <summary>
    /// End-to-end test of directive processing. More cases are covered by faster unit tests below.
    /// </summary>
    [Fact]
    public void ProcessingSucceeds()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:sdk Aspire.Hosting.Sdk/9.1.0
            Console.WriteLine();
            """);

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path).Should().HaveSubtree("""
            Program.cs
            Program.csproj
            """);

        File.ReadAllText(Path.Join(testInstance.Path, "Program.cs"))
            .Should().Be("Console.WriteLine();");

        File.ReadAllText(Path.Join(testInstance.Path, "Program.csproj"))
            .Should().Be($"""
                <Project Sdk="Aspire.Hosting.Sdk/9.1.0">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>

                </Project>

                """);
    }

    [Fact]
    public void MultipleFiles_SingleEntryPoint()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        string programContent = "Console.WriteLine(Util.GetMessage());";
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), $"""
            #:sdk Aspire.Hosting.Sdk/9.1.0
            #:property Prop1 ValueProgram
            {programContent}
            """);
        string utilContent = """
            static class Util
            {
                public static string GetMessage()
                {
                    return "String from Util";
                }
            }
            """;
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), $"""
            #:property Prop1 ValueUtil
            {utilContent}
            """);

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path).Should().HaveSubtree("""
            Program.cs
            Program.csproj
            Util.cs
            """);

        File.ReadAllText(Path.Join(testInstance.Path, "Program.cs"))
            .Should().Be(programContent);

        File.ReadAllText(Path.Join(testInstance.Path, "Util.cs"))
            .Should().Be(utilContent);

        File.ReadAllText(Path.Join(testInstance.Path, "Program.csproj"))
            .Should().Be($"""
                <Project Sdk="Aspire.Hosting.Sdk/9.1.0">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>

                  <PropertyGroup>
                    <Prop1>ValueUtil</Prop1>
                    <Prop1>ValueProgram</Prop1>
                  </PropertyGroup>

                </Project>

                """);
    }

    [Fact]
    public void MultipleFiles_MultipleEntryPoints()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        string program1Content = """Console.WriteLine("1 " + Util.GetMessage());""";
        File.WriteAllText(Path.Join(testInstance.Path, "Program1.cs"), $"""
            #:property Prop1 ValueProgram1
            {program1Content}
            """);
        string program2Content = """Console.WriteLine("2 " + Util.GetMessage());""";
        File.WriteAllText(Path.Join(testInstance.Path, "Program2.cs"), $"""
            #:property Prop1 ValueProgram2
            {program2Content}
            """);
        string utilContent = """
            static class Util
            {
                public static string GetMessage()
                {
                    return "String from Util";
                }
            }
            """;
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), $"""
            #:property Prop1 ValueUtil
            {utilContent}
            """);

        // Run the file-based programs.
        string expectedOutput1 = "1 String from Util";
        string expectedOutput2 = "2 String from Util";
        new DotnetCommand(Log, "run", "Program1.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput1);
        new DotnetCommand(Log, "run", "Program2.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput2);

        // Cannot convert a single entry point, must convert the whole directory.
        new DotnetCommand(Log, "project", "convert", "Program1.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.DirectoryMustBeSpecified, Path.Join(testInstance.Path, "Program1.cs")));

        new DotnetCommand(Log, "project", "convert", ".")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path).Should().HaveSubtree("""
            Program1/
            Program1/Program1.cs
            Program1/Program1.csproj
            Program2/
            Program2/Program2.cs
            Program2/Program2.csproj
            Shared/
            Shared/Util.cs
            """);

        File.ReadAllText(Path.Join(testInstance.Path, "Program1", "Program1.cs"))
            .Should().Be(program1Content);

        File.ReadAllText(Path.Join(testInstance.Path, "Program1", "Program1.csproj"))
            .Should().Be($"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>

                  <PropertyGroup>
                    <Prop1>ValueUtil</Prop1>
                    <Prop1>ValueProgram1</Prop1>
                  </PropertyGroup>

                  <ItemGroup>
                    <Compile Include="..\Shared\**\*.cs" />
                  </ItemGroup>

                </Project>

                """);

        File.ReadAllText(Path.Join(testInstance.Path, "Program2", "Program2.cs"))
            .Should().Be(program2Content);

        File.ReadAllText(Path.Join(testInstance.Path, "Program2", "Program2.csproj"))
            .Should().Be($"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>

                  <PropertyGroup>
                    <Prop1>ValueUtil</Prop1>
                    <Prop1>ValueProgram2</Prop1>
                  </PropertyGroup>

                  <ItemGroup>
                    <Compile Include="..\Shared\**\*.cs" />
                  </ItemGroup>

                </Project>

                """);

        File.ReadAllText(Path.Join(testInstance.Path, "Shared", "Util.cs"))
            .Should().Be(utilContent);

        // Run the converted programs.
        new DotnetCommand(Log, "run", "--project", "Program1")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput1);
        new DotnetCommand(Log, "run", "--project", "Program2")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput2);
    }

    [Fact]
    public void MultipleFiles_MultipleEntryPoints_NestedFiles()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program1.cs"), "Console.Write(1);");
        File.WriteAllText(Path.Join(testInstance.Path, "Program2.cs"), "Console.Write(2);");
        Directory.CreateDirectory(Path.Join(testInstance.Path, "Dir"));
        File.WriteAllText(Path.Join(testInstance.Path, "Dir", "Util.cs"), """
            #:sdk Test
            class C;
            """);

        new DotnetCommand(Log, "project", "convert", ".")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path).Should().HaveSubtree("""
            Program1/
            Program1/Program1.cs
            Program1/Program1.csproj
            Program2/
            Program2/Program2.cs
            Program2/Program2.csproj
            Shared/
            Shared/Dir/
            Shared/Dir/Util.cs
            """);

        File.ReadAllText(Path.Join(testInstance.Path, "Shared", "Dir", "Util.cs"))
            .Should().Be("class C;");
    }

    [Fact]
    public void MultipleFiles_MultipleEntryPoints_SharedConflict()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program1.cs"), "Console.Write(1);");
        File.WriteAllText(Path.Join(testInstance.Path, "Shared.cs"), "Console.Write(2);");
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), "class C;");

        new DotnetCommand(Log, "project", "convert", ".")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.SharedDirectoryNameConflicts, "Shared"));

        new DotnetCommand(Log, "project", "convert", ".", "--shared-directory-name", "program1")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.SharedDirectoryNameConflicts, "program1"));

        new DotnetCommand(Log, "project", "convert", ".", "--shared-directory-name", "Shared1")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path).Should().HaveSubtree("""
            Program1/
            Program1/Program1.cs
            Program1/Program1.csproj
            Shared/
            Shared/Shared.cs
            Shared/Shared.csproj
            Shared1/
            Shared1/Util.cs
            """);

        string expectedCsproj = $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>

              <ItemGroup>
                <Compile Include="..\Shared1\**\*.cs" />
              </ItemGroup>

            </Project>

            """;

        File.ReadAllText(Path.Join(testInstance.Path, "Program1", "Program1.csproj")).Should().Be(expectedCsproj);
        File.ReadAllText(Path.Join(testInstance.Path, "Shared", "Shared.csproj")).Should().Be(expectedCsproj);
    }

    [Fact]
    public void MultipleFiles_MultipleEntryPoints_SharedNamedUtil()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program1.cs"), "Console.Write(1);");
        File.WriteAllText(Path.Join(testInstance.Path, "Program2.cs"), "Console.Write(2);");
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), "class C;");

        new DotnetCommand(Log, "project", "convert", ".", "--shared-directory-name", "Util")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path).Should().HaveSubtree("""
            Program1/
            Program1/Program1.cs
            Program1/Program1.csproj
            Program2/
            Program2/Program2.cs
            Program2/Program2.csproj
            Util/
            Util/Util.cs
            """);
    }

    [Fact]
    public void MultipleFiles_MultipleEntryPoints_SharedNamedLikeExistingFolder()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program1.cs"), "Console.Write(1);");
        File.WriteAllText(Path.Join(testInstance.Path, "Program2.cs"), "Console.Write(2);");
        Directory.CreateDirectory(Path.Join(testInstance.Path, "Shared"));
        File.WriteAllText(Path.Join(testInstance.Path, "Shared", "Util.cs"), "class C;");

        new DotnetCommand(Log, "project", "convert", ".", "--shared-directory-name", "Shared")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path).Should().HaveSubtree("""
            Program1/
            Program1/Program1.cs
            Program1/Program1.csproj
            Program2/
            Program2/Program2.cs
            Program2/Program2.csproj
            Shared/
            Shared/Shared/
            Shared/Shared/Util.cs
            """);
    }

    [Fact]
    public void MultipleFiles_MultipleEntryPoints_NoSharedNeeded()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program1.cs"), "Console.Write(1);");
        File.WriteAllText(Path.Join(testInstance.Path, "Program2.cs"), "Console.Write(2);");

        new DotnetCommand(Log, "project", "convert", ".")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path).Should().HaveSubtree("""
            Program1/
            Program1/Program1.cs
            Program1/Program1.csproj
            Program2/
            Program2/Program2.cs
            Program2/Program2.csproj
            """);
    }

    [Fact]
    public void MultipleFiles_MultipleEntryPoints_NoSharedNeeded_ConflictDoesNotMatter()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program1.cs"), "Console.Write(1);");
        File.WriteAllText(Path.Join(testInstance.Path, "Shared.cs"), "Console.Write(2);");

        new DotnetCommand(Log, "project", "convert", ".")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path).Should().HaveSubtree("""
            Program1/
            Program1/Program1.cs
            Program1/Program1.csproj
            Shared/
            Shared/Shared.cs
            Shared/Shared.csproj
            """);
    }

    [Fact]
    public void MultipleFiles_MultipleEntryPoints_NonCSharpFiles()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program1.cs"), "Console.Write(1);");
        File.WriteAllText(Path.Join(testInstance.Path, "Program2.cs"), "Console.Write(2);");
        File.WriteAllText(Path.Join(testInstance.Path, "Strings.resx"), "<strings />");
        Directory.CreateDirectory(Path.Join(testInstance.Path, "Folder1"));
        Directory.CreateDirectory(Path.Join(testInstance.Path, "Folder2"));
        File.WriteAllText(Path.Join(testInstance.Path, "Folder2/Strings.resx"), "<strings />");

        new DotnetCommand(Log, "project", "convert", ".")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path).Should().HaveSubtree("""
            Program1/
            Program1/Program1.cs
            Program1/Program1.csproj
            Program2/
            Program2/Program2.cs
            Program2/Program2.csproj
            Shared/
            Shared/Folder1/
            Shared/Folder2/
            Shared/Folder2/Strings.resx
            Shared/Strings.resx
            """);
    }

    [Fact]
    public void Directives()
    {
        VerifyConversion(
            inputCSharp: """
                #!/program
                #:sdk Microsoft.NET.Sdk
                #:sdk Aspire.Hosting.Sdk 9.1.0
                #:property TargetFramework net11.0
                #:package System.CommandLine 2.0.0-beta4.22272.1
                #:property LangVersion preview
                Console.WriteLine();
                """,
            expectedProject: $"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <Sdk Name="Aspire.Hosting.Sdk" Version="9.1.0" />

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>

                  <PropertyGroup>
                    <TargetFramework>net11.0</TargetFramework>
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

    [Fact]
    public void Directives_Virtual()
    {
        VerifyConversion(
            inputCSharp: """
                #!/program
                #:sdk Microsoft.NET.Sdk
                #:sdk Aspire.Hosting.Sdk 9.1.0
                #:property TargetFramework net11.0
                #:package System.CommandLine 2.0.0-beta4.22272.1
                #:property LangVersion preview
                Console.WriteLine();
                """,
            isVirtualProject: true,
            excludeCompileItems: ["/test1", "/test2"],
            expectedProject: $"""
                <Project>

                  <PropertyGroup>
                    <IncludeProjectNameInArtifactsPaths>false</IncludeProjectNameInArtifactsPaths>
                    <ArtifactsPath>/artifacts</ArtifactsPath>
                  </PropertyGroup>

                  <!-- We need to explicitly import Sdk props/targets so we can override the targets below. -->
                  <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />
                  <Import Project="Sdk.props" Sdk="Aspire.Hosting.Sdk/9.1.0" />

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>

                  <PropertyGroup>
                    <TargetFramework>net11.0</TargetFramework>
                    <LangVersion>preview</LangVersion>
                  </PropertyGroup>

                  <PropertyGroup>
                    <Features>$(Features);FileBasedProgram</Features>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
                  </ItemGroup>

                  <ItemGroup>
                    <Compile Remove="/test1" />
                    <Compile Remove="/test2" />
                  </ItemGroup>

                  <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />
                  <Import Project="Sdk.targets" Sdk="Aspire.Hosting.Sdk/9.1.0" />

                  <!--
                    Override targets which don't work with project files that are not present on disk.
                    See https://github.com/NuGet/Home/issues/14148.
                  -->

                  <Target Name="_FilterRestoreGraphProjectInputItems"
                          DependsOnTargets="_LoadRestoreGraphEntryPoints"
                          Returns="@(FilteredRestoreGraphProjectInputItems)">
                    <ItemGroup>
                      <FilteredRestoreGraphProjectInputItems Include="@(RestoreGraphProjectInputItems)" />
                    </ItemGroup>
                  </Target>

                  <Target Name="_GetAllRestoreProjectPathItems"
                          DependsOnTargets="_FilterRestoreGraphProjectInputItems"
                          Returns="@(_RestoreProjectPathItems)">
                    <ItemGroup>
                      <_RestoreProjectPathItems Include="@(FilteredRestoreGraphProjectInputItems)" />
                    </ItemGroup>
                  </Target>

                  <Target Name="_GenerateRestoreGraph"
                          DependsOnTargets="_FilterRestoreGraphProjectInputItems;_GetAllRestoreProjectPathItems;_GenerateRestoreGraphProjectEntry;_GenerateProjectRestoreGraph"
                          Returns="@(_RestoreGraphEntry)">
                    <!-- Output from dependency _GenerateRestoreGraphProjectEntry and _GenerateProjectRestoreGraph -->
                  </Target>

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
                #:package MyPackage $(MyProp)
                #:property MyProp MyValue
                """,
            expectedProject: $"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>

                  <PropertyGroup>
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
    public void Directives_Separators()
    {
        VerifyConversion(
            inputCSharp: """
                #:property Prop1   One=a/b
                #:property Prop2   Two/a=b
                #:sdk First 1.0=a/b
                #:sdk Second 2.0/a=b
                #:sdk Third 3.0=a/b
                #:package P1 1.0/a=b
                #:package P2 2.0/a=b
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
                  </PropertyGroup>

                  <PropertyGroup>
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
            expectedWildcardPattern: string.Format(CliCommandStrings.UnrecognizedDirective, directive, "/app/Program.cs:2"));
    }

    [Fact]
    public void Directives_Empty()
    {
        VerifyConversionThrows(
            inputCSharp: """
                #:
                #:sdk Test
                """,
            expectedWildcardPattern: string.Format(CliCommandStrings.UnrecognizedDirective, "", "/app/Program.cs:1"));
    }

    [Theory, CombinatorialData]
    public void Directives_EmptyName(
        [CombinatorialValues("sdk", "property", "package")] string directive,
        [CombinatorialValues(" ", "")] string value)
    {
        VerifyConversionThrows(
            inputCSharp: $"""
                #:{directive}{value}
                """,
            expectedWildcardPattern: string.Format(CliCommandStrings.MissingDirectiveName, directive, "/app/Program.cs:1"));
    }

    [Fact]
    public void Directives_MissingPropertyValue()
    {
        VerifyConversionThrows(
            inputCSharp: """
                #:property Test
                """,
            expectedWildcardPattern: string.Format(CliCommandStrings.PropertyDirectiveMissingParts, "/app/Program.cs:1"));
    }

    [Fact]
    public void Directives_InvalidPropertyName()
    {
        VerifyConversionThrows(
            inputCSharp: """
                #:property Name" Value
                """,
            expectedWildcardPattern: string.Format(CliCommandStrings.PropertyDirectiveInvalidName, "/app/Program.cs:1", """
                The '"' character, hexadecimal value 0x22, cannot be included in a name.
                """));
    }

    [Fact]
    public void Directives_Escaping()
    {
        VerifyConversion(
            inputCSharp: """
                #:property Prop <test">
                #:sdk <test"> ="<>test
                #:package <test"> ="<>test
                """,
            expectedProject: $"""
                <Project Sdk="&lt;test&quot;&gt;/=&quot;&lt;&gt;test">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>

                  <PropertyGroup>
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
                #:property Name   Value   
                #:property NugetPackageDescription "My package with spaces"
                 #  !  /test
                  #!  /program   x   
                 # :property Name Value
                """,
            expectedProject: $"""
                <Project Sdk="TestSdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>

                  <PropertyGroup>
                    <Name>Value</Name>
                    <NugetPackageDescription>&quot;My package with spaces&quot;</NugetPackageDescription>
                  </PropertyGroup>

                </Project>

                """,
            expectedCSharp: """
                 #  !  /test
                  #!  /program   x   
                 # :property Name Value
                """);
    }

    [Fact]
    public void Directives_Whitespace_Invalid()
    {
        VerifyConversionThrows(
            inputCSharp: $"""
                #:   property   Name{'\t'}     Value
                """,
            expectedWildcardPattern: string.Format(CliCommandStrings.PropertyDirectiveInvalidName, "/app/Program.cs:1",
                "The '\t' character, hexadecimal value 0x09, cannot be included in a name."));
    }

    /// <summary>
    /// <c>#:</c> directives after C# code are ignored.
    /// </summary>
    [Fact]
    public void Directives_AfterToken()
    {
        string source = """
            #:property Prop 1
            #define X
            #:property Prop 2
            Console.WriteLine();
            #:property Prop 3
            """;

        VerifyConversionThrows(
            inputCSharp: source,
            expectedWildcardPattern: string.Format(CliCommandStrings.CannotConvertDirective, "/app/Program.cs:5"));

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
                  </PropertyGroup>

                  <PropertyGroup>
                    <Prop>1</Prop>
                    <Prop>2</Prop>
                  </PropertyGroup>

                </Project>

                """,
            expectedCSharp: """
                #define X
                Console.WriteLine();
                #:property Prop 3
                """);
    }

    /// <summary>
    /// <c>#:</c> directives after <c>#if</c> are ignored.
    /// </summary>
    [Fact]
    public void Directives_AfterIf()
    {
        string source = """
            #:property Prop 1
            #define X
            #:property Prop 2
            #if X
            #:property Prop 3
            #endif
            #:property Prop 4
            """;

        VerifyConversionThrows(
            inputCSharp: source,
            expectedWildcardPattern: string.Format(CliCommandStrings.CannotConvertDirective, "/app/Program.cs:5"));

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
                  </PropertyGroup>

                  <PropertyGroup>
                    <Prop>1</Prop>
                    <Prop>2</Prop>
                  </PropertyGroup>

                </Project>

                """,
            expectedCSharp: """
                #define X
                #if X
                #:property Prop 3
                #endif
                #:property Prop 4
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
                #:property Prop 1
                #:property Prop 2
                Console.Write();
                """,
            expectedProject: $"""
                <Project Sdk="MySdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>

                  <PropertyGroup>
                    <Prop>1</Prop>
                    <Prop>2</Prop>
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

    private static void Convert(
        string inputCSharp,
        out string actualProject,
        out string? actualCSharp,
        bool force,
        bool isVirtualProject,
        ImmutableArray<string> excludeCompileItems)
    {
        var sourceFile = new SourceFile("/app/Program.cs", SourceText.From(inputCSharp, Encoding.UTF8));

        var directives = VirtualProjectBuildingCommand.FindDirectives(sourceFile, reportErrors: !isVirtualProject && !force);

        var projectWriter = new StringWriter();

        VirtualProjectBuildingCommand.WriteProjectFile(
            projectWriter,
            directives,
            options: isVirtualProject
                ? new ProjectWritingOptions.Virtual
                {
                    ArtifactsPath = "/artifacts",
                    ExcludeCompileItems = excludeCompileItems,
                }
                : new ProjectWritingOptions.Converted
                {
                    SharedDirectoryName = null,
                });

        actualProject = projectWriter.ToString();
        actualCSharp = VirtualProjectBuildingCommand.RemoveDirectivesFromFile(directives, sourceFile.Text)?.ToString();
    }

    /// <param name="expectedCSharp">
    /// <see langword="null"/> means the conversion should not touch the C# content.
    /// </param>
    private static void VerifyConversion(
        string inputCSharp,
        string expectedProject,
        string? expectedCSharp,
        bool force = false,
        bool isVirtualProject = false,
        ImmutableArray<string> excludeCompileItems = default)
    {
        Convert(
            inputCSharp,
            out var actualProject,
            out var actualCSharp,
            force: force,
            isVirtualProject: isVirtualProject,
            excludeCompileItems: excludeCompileItems);
        actualProject.Should().Be(expectedProject);
        actualCSharp.Should().Be(expectedCSharp);
    }

    private static void VerifyConversionThrows(string inputCSharp, string expectedWildcardPattern)
    {
        var convert = () => Convert(
            inputCSharp,
            out _,
            out _,
            force: false,
            isVirtualProject: false,
            excludeCompileItems: default);
        convert.Should().Throw<GracefulException>().WithMessage(expectedWildcardPattern);
    }
}
