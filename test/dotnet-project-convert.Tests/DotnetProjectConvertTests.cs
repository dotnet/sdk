// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

        var dotnetProjectConvertProject = Directory.EnumerateFiles(Path.Join(dotnetProjectConvert, "Program"), "*.csproj").Single();

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
    public void DirectoryAlreadyExists()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        Directory.CreateDirectory(Path.Join(testInstance.Path, "MyApp"));
        File.WriteAllText(Path.Join(testInstance.Path, "MyApp.cs"), "Console.WriteLine();");

        new DotnetCommand(Log, "project", "convert", "MyApp.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining("The target directory already exists");
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
        Directory.CreateDirectory(Path.Join(testInstance.Path, "SomeOutput"));
        File.WriteAllText(Path.Join(testInstance.Path, "MyApp.cs"), "Console.WriteLine();");

        new DotnetCommand(Log, "project", "convert", "MyApp.cs", "-o", "SomeOutput")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining("The target directory already exists");
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
            .EnumerateDirectories().Select(d => d.Name).Order()
            .Should().BeEquivalentTo(["Program1"]);

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
            .And.HaveStdErrContaining("Required argument missing for command");
    }

    [Fact]
    public void NonExistentFile()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();

        new DotnetCommand(Log, "project", "convert", "NotHere.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining("The specified file must exist");
    }

    [Fact]
    public void NonCSharpFile()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.vb"), "");

        new DotnetCommand(Log, "project", "convert", "Program.vb")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining("The specified file must exist and have '.cs' file extension");
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

        new DirectoryInfo(Path.Join(testInstance.Path, "app", "Program"))
            .EnumerateFileSystemInfos().Select(f => f.Name).Order()
            .Should().BeEquivalentTo(["Program.csproj", "Program.cs"]);
    }
}
