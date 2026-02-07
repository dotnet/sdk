// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.Watch.UnitTests;

public class Program_GetProjectOptionsTests(ITestOutputHelper output)
{
    private readonly TestAssetsManager _testAssetManager = new(output);
    private readonly TestLogger _testLogger = new();

    private string CreateTempDirectory([CallerMemberName] string? callingMethod = null, string? identifier = null)
        => _testAssetManager.CreateTestDirectory(callingMethod, identifier).Path;

    private CommandLineOptions ParseOptions(string[] args)
    {
        var output = new StringWriter();
        var options = CommandLineOptions.Parse(args, _testLogger, output: output, errorCode: out _);
        Assert.NotNull(options);
        return options;
    }

    [Fact]
    public void ExplicitProjectPath()
    {
        var tempDir = CreateTempDirectory();
        var projectPath = Path.Combine(tempDir, "Test.csproj");
        File.WriteAllText(projectPath, "<Project></Project>");

        var options = ParseOptions(["--project", projectPath]);
        var result = Program.GetMainProjectOptions(options, tempDir, _testLogger);

        Assert.NotNull(result);
        Assert.Equal(projectPath, result.Representation.PhysicalPath);
        Assert.Null(result.Representation.EntryPointFilePath);
        Assert.Equal(tempDir, result.WorkingDirectory);
        Assert.True(result.IsMainProject);
    }

    [Fact]
    public void ProjectInWorkingDirectory()
    {
        var tempDir = CreateTempDirectory();
        var projectPath = Path.Combine(tempDir, "MyApp.csproj");
        File.WriteAllText(projectPath, "<Project></Project>");

        var options = ParseOptions([]);
        var result = Program.GetMainProjectOptions(options, tempDir, _testLogger);

        Assert.NotNull(result);
        Assert.Equal(projectPath, result.Representation.PhysicalPath);
        Assert.Null(result.Representation.EntryPointFilePath);
    }

    [Fact]
    public void MultipleProjects()
    {
        var tempDir = CreateTempDirectory();
        File.WriteAllText(Path.Combine(tempDir, "App1.csproj"), "<Project></Project>");
        File.WriteAllText(Path.Combine(tempDir, "App2.csproj"), "<Project></Project>");

        var options = ParseOptions([]);
        var result = Program.GetMainProjectOptions(options, tempDir, _testLogger);

        Assert.Null(result);
        AssertEx.SequenceEqual(
            [$"[Error] {string.Format(Resources.Error_MultipleProjectsFound, tempDir)}"],
            _testLogger.GetAndClearMessages());
    }

    [Fact]
    public void NonExistentProject()
    {
        var tempDir = CreateTempDirectory();
        var projectPath = Path.Combine(tempDir, "NonExistent.csproj");
        var options = ParseOptions(["--project", projectPath]);
        var result = Program.GetMainProjectOptions(options, tempDir, _testLogger);

        Assert.Null(result);
        AssertEx.SequenceEqual(
            [$"[Error] {string.Format(Resources.Error_ProjectPath_NotFound, projectPath)}"],
            _testLogger.GetAndClearMessages());
    }

    [Fact]
    public void NoProjectsInDirectoryAndNoCSharpFile()
    {
        var tempDir = CreateTempDirectory();
        var emptyDir = Path.Combine(tempDir, "empty");
        Directory.CreateDirectory(emptyDir);

        var options = ParseOptions([]);
        var result = Program.GetMainProjectOptions(options, emptyDir, _testLogger);

        Assert.Null(result);
        AssertEx.SequenceEqual(
            [$"[Error] {string.Format(Resources.Could_not_find_msbuild_project_file_in_0, emptyDir)}"],
            _testLogger.GetAndClearMessages());
    }

    [Fact]
    public void ProjectDirectory()
    {
        var tempDir = CreateTempDirectory();
        var subDir = Path.Combine(tempDir, "subdir");
        Directory.CreateDirectory(subDir);
        var projectPath = Path.Combine(subDir, "SubApp.csproj");
        File.WriteAllText(projectPath, "<Project></Project>");

        var options = ParseOptions(["--project", subDir]);
        var result = Program.GetMainProjectOptions(options, tempDir, _testLogger);

        Assert.NotNull(result);
        Assert.Equal(projectPath, result.Representation.PhysicalPath);
    }

    [Theory]
    [InlineData("csproj")]
    [InlineData("fsproj")]
    [InlineData("vbproj")]
    [InlineData("proj")]
    public void ProjectFile_AcceptedExtension(string projExtension)
    {
        var tempDir = CreateTempDirectory(projExtension);
        var projectPath = Path.Combine(tempDir, $"Test.{projExtension}");
        File.WriteAllText(projectPath, "<Project></Project>");

        var options = ParseOptions([]);
        var result = Program.GetMainProjectOptions(options, tempDir, _testLogger);

        Assert.NotNull(result);
        Assert.Equal(projectPath, result.Representation.PhysicalPath);
    }

    [Theory]
    [InlineData("shproj")]
    public void ProjectFile_RejectedExtension(string projExtension)
    {
        var tempDir = CreateTempDirectory(projExtension);
        var projectPath = Path.Combine(tempDir, $"Test.{projExtension}");
        File.WriteAllText(projectPath, "<Project></Project>");

        var options = ParseOptions([]);
        var result = Program.GetMainProjectOptions(options, tempDir, _testLogger);

        Assert.Null(result);
        AssertEx.SequenceEqual(
            [$"[Error] {string.Format(Resources.Could_not_find_msbuild_project_file_in_0, tempDir)}"],
            _testLogger.GetAndClearMessages());
    }

    [Fact]
    public void InvalidFilePath()
    {
        var tempDir = CreateTempDirectory();
        var invalidPath = "invalid\0path.cs";

        var options = ParseOptions(["--file", invalidPath]);
        var result = Program.GetMainProjectOptions(options, tempDir, _testLogger);

        string message;
        try
        {
            Path.GetFullPath(invalidPath);
            message = "";
        }
        catch (Exception e)
        {
            message = e.Message;
        }

        Assert.Null(result);
        AssertEx.SequenceEqual(
            [$"[Error] {string.Format(Resources.The_specified_path_0_is_invalid_1, invalidPath, message)}"],
            _testLogger.GetAndClearMessages());
    }

    [Fact]
    public void FilePathOption()
    {
        var tempDir = CreateTempDirectory();
        var csFilePath = Path.Combine(tempDir, "Program.cs");
        File.WriteAllText(csFilePath, "Console.WriteLine(\"Hello\");");

        var options = ParseOptions(["--file", csFilePath]);
        var result = Program.GetMainProjectOptions(options, tempDir, _testLogger);

        Assert.NotNull(result);
        Assert.Equal(csFilePath, result.Representation.EntryPointFilePath);
        Assert.Null(result.Representation.PhysicalPath);
    }

    [Fact]
    public void CSharpFileSpecifiedAsArgument()
    {
        var tempDir = CreateTempDirectory();
        var csFilePath = Path.Combine(tempDir, "App.cs");
        File.WriteAllText(csFilePath, "Console.WriteLine(\"Hello\");");

        // dotnet watch App.cs
        var options = ParseOptions([csFilePath]);
        var result = Program.GetMainProjectOptions(options, tempDir, _testLogger);

        Assert.NotNull(result);
        Assert.Equal(csFilePath, result.Representation.EntryPointFilePath);
    }

    [Fact]
    public void FileWithShebangSpecifiedAsArgument()
    {
        var tempDir = CreateTempDirectory();
        var filePath = Path.Combine(tempDir, "App.txt");
        File.WriteAllText(filePath, "#!");

        // dotnet watch App.txt
        var options = ParseOptions([filePath]);
        var result = Program.GetMainProjectOptions(options, tempDir, _testLogger);

        Assert.NotNull(result);
        Assert.Equal(filePath, result.Representation.EntryPointFilePath);
    }

    [Fact]
    public void RelativeProjectPath()
    {
        var tempDir = CreateTempDirectory();
        var subDir = Path.Combine(tempDir, "subdir");
        Directory.CreateDirectory(subDir);
        var projectPath = Path.Combine(subDir, "Test.csproj");
        File.WriteAllText(projectPath, "<Project></Project>");

        var options = ParseOptions(["--project", "subdir/Test.csproj"]);
        var result = Program.GetMainProjectOptions(options, tempDir, _testLogger);

        Assert.NotNull(result);
        Assert.Equal(projectPath, result.Representation.PhysicalPath);
    }

    [Fact]
    public void RelativeFilePath()
    {
        var tempDir = CreateTempDirectory();
        var csFilePath = Path.Combine(tempDir, "Script.cs");
        File.WriteAllText(csFilePath, "Console.WriteLine(\"Hello\");");

        var options = ParseOptions(["--file", "Script.cs"]);
        var result = Program.GetMainProjectOptions(options, tempDir, _testLogger);

        Assert.NotNull(result);
        Assert.Equal(csFilePath, result.Representation.EntryPointFilePath);
    }

    [Fact]
    public void FilePathOptionTakesPrecedenceOverProjectPath()
    {
        var tempDir = CreateTempDirectory();
        var projectPath = Path.Combine(tempDir, "Test.csproj");
        File.WriteAllText(projectPath, "<Project></Project>");
        var csFilePath = Path.Combine(tempDir, "Script.cs");
        File.WriteAllText(csFilePath, "Console.WriteLine(\"Hello\");");

        var options = ParseOptions(["--file", csFilePath]);
        var result = Program.GetMainProjectOptions(options, tempDir, _testLogger);

        Assert.NotNull(result);
        Assert.Equal(csFilePath, result.Representation.EntryPointFilePath);
        Assert.Null(result.Representation.PhysicalPath);
    }
}
