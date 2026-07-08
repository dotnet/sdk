// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

extern alias MSTestFramework;

using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class Program_GetProjectOptionsTests
{
    public TestContext TestContext { get; set; } = null!;
    private DualOutputHelper? _output;
    private DualOutputHelper Output => _output ??= new(new MSTestFramework::Microsoft.NET.TestFramework.TestContextOutputHelper(TestContext));
    private TestAssetsManager? _testAssetManager;
    private TestAssetsManager TestAssetManager => _testAssetManager ??= new(Output);
    private readonly TestLogger _testLogger = new();

    private string CreateTempDirectory([CallerMemberName] string? callingMethod = null, string? identifier = null)
        => TestAssetManager.CreateTestDirectory(callingMethod, identifier).Path;

    private CommandLineOptions ParseOptions(string[] args)
    {
        var output = new StringWriter();
        var options = CommandLineOptions.Parse(args, _testLogger, output: output, errorCode: out _);
        Assert.IsNotNull(options);
        return options;
    }

    [TestMethod]
    public void ExplicitProjectPath()
    {
        var tempDir = CreateTempDirectory();
        var projectPath = Path.Combine(tempDir, "Test.csproj");
        File.WriteAllText(projectPath, "<Project></Project>");

        var options = ParseOptions(["--project", projectPath]);
        var result = Program.GetMainProjectOptions(options, tempDir, _testLogger);

        Assert.IsNotNull(result);
        Assert.AreEqual(projectPath, result.Representation.PhysicalPath);
        Assert.IsNull(result.Representation.EntryPointFilePath);
        Assert.AreEqual(tempDir, result.WorkingDirectory);
        Assert.IsTrue(result.IsMainProject);
    }

    [TestMethod]
    public void ProjectInWorkingDirectory()
    {
        var tempDir = CreateTempDirectory();
        var projectPath = Path.Combine(tempDir, "MyApp.csproj");
        File.WriteAllText(projectPath, "<Project></Project>");

        var options = ParseOptions([]);
        var result = Program.GetMainProjectOptions(options, tempDir, _testLogger);

        Assert.IsNotNull(result);
        Assert.AreEqual(projectPath, result.Representation.PhysicalPath);
        Assert.IsNull(result.Representation.EntryPointFilePath);
    }

    [TestMethod]
    public void MultipleProjects()
    {
        var tempDir = CreateTempDirectory();
        File.WriteAllText(Path.Combine(tempDir, "App1.csproj"), "<Project></Project>");
        File.WriteAllText(Path.Combine(tempDir, "App2.csproj"), "<Project></Project>");

        var options = ParseOptions([]);
        var result = Program.GetMainProjectOptions(options, tempDir, _testLogger);

        Assert.IsNull(result);
        AssertEx.SequenceEqual(
            [$"[Error] {string.Format(Resources.Error_MultipleProjectsFound, tempDir)}"],
            _testLogger.GetAndClearMessages());
    }

    [TestMethod]
    public void NonExistentProject()
    {
        var tempDir = CreateTempDirectory();
        var projectPath = Path.Combine(tempDir, "NonExistent.csproj");
        var options = ParseOptions(["--project", projectPath]);
        var result = Program.GetMainProjectOptions(options, tempDir, _testLogger);

        Assert.IsNull(result);
        AssertEx.SequenceEqual(
            [$"[Error] {string.Format(Resources.Error_ProjectPath_NotFound, projectPath)}"],
            _testLogger.GetAndClearMessages());
    }

    [TestMethod]
    public void NoProjectsInDirectoryAndNoCSharpFile()
    {
        var tempDir = CreateTempDirectory();
        var emptyDir = Path.Combine(tempDir, "empty");
        Directory.CreateDirectory(emptyDir);

        var options = ParseOptions([]);
        var result = Program.GetMainProjectOptions(options, emptyDir, _testLogger);

        Assert.IsNull(result);
        AssertEx.SequenceEqual(
            [$"[Error] {string.Format(Resources.Could_not_find_msbuild_project_file_in_0, emptyDir)}"],
            _testLogger.GetAndClearMessages());
    }

    [TestMethod]
    public void ProjectDirectory()
    {
        var tempDir = CreateTempDirectory();
        var subDir = Path.Combine(tempDir, "subdir");
        Directory.CreateDirectory(subDir);
        var projectPath = Path.Combine(subDir, "SubApp.csproj");
        File.WriteAllText(projectPath, "<Project></Project>");

        var options = ParseOptions(["--project", subDir]);
        var result = Program.GetMainProjectOptions(options, tempDir, _testLogger);

        Assert.IsNotNull(result);
        Assert.AreEqual(projectPath, result.Representation.PhysicalPath);
    }

    [TestMethod]
    [DataRow("csproj")]
    [DataRow("fsproj")]
    [DataRow("vbproj")]
    [DataRow("proj")]
    public void ProjectFile_AcceptedExtension(string projExtension)
    {
        var tempDir = CreateTempDirectory(projExtension);
        var projectPath = Path.Combine(tempDir, $"Test.{projExtension}");
        File.WriteAllText(projectPath, "<Project></Project>");

        var options = ParseOptions([]);
        var result = Program.GetMainProjectOptions(options, tempDir, _testLogger);

        Assert.IsNotNull(result);
        Assert.AreEqual(projectPath, result.Representation.PhysicalPath);
    }

    [TestMethod]
    [DataRow("shproj")]
    public void ProjectFile_RejectedExtension(string projExtension)
    {
        var tempDir = CreateTempDirectory(projExtension);
        var projectPath = Path.Combine(tempDir, $"Test.{projExtension}");
        File.WriteAllText(projectPath, "<Project></Project>");

        var options = ParseOptions([]);
        var result = Program.GetMainProjectOptions(options, tempDir, _testLogger);

        Assert.IsNull(result);
        AssertEx.SequenceEqual(
            [$"[Error] {string.Format(Resources.Could_not_find_msbuild_project_file_in_0, tempDir)}"],
            _testLogger.GetAndClearMessages());
    }

    [TestMethod]
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

        Assert.IsNull(result);
        AssertEx.SequenceEqual(
            [$"[Error] {string.Format(Resources.The_specified_path_0_is_invalid_1, invalidPath, message)}"],
            _testLogger.GetAndClearMessages());
    }

    [TestMethod]
    public void FilePathOption()
    {
        var tempDir = CreateTempDirectory();
        var csFilePath = Path.Combine(tempDir, "Program.cs");
        File.WriteAllText(csFilePath, "Console.WriteLine(\"Hello\");");

        var options = ParseOptions(["--file", csFilePath]);
        var result = Program.GetMainProjectOptions(options, tempDir, _testLogger);

        Assert.IsNotNull(result);
        Assert.AreEqual(csFilePath, result.Representation.EntryPointFilePath);
        Assert.IsNull(result.Representation.PhysicalPath);
    }

    [TestMethod]
    public void CSharpFileSpecifiedAsArgument()
    {
        var tempDir = CreateTempDirectory();
        var csFilePath = Path.Combine(tempDir, "App.cs");
        File.WriteAllText(csFilePath, "Console.WriteLine(\"Hello\");");

        // dotnet watch App.cs
        var options = ParseOptions([csFilePath]);
        var result = Program.GetMainProjectOptions(options, tempDir, _testLogger);

        Assert.IsNotNull(result);
        Assert.AreEqual(csFilePath, result.Representation.EntryPointFilePath);
    }

    [TestMethod]
    public void FileWithShebangSpecifiedAsArgument()
    {
        var tempDir = CreateTempDirectory();
        var filePath = Path.Combine(tempDir, "App.txt");
        File.WriteAllText(filePath, "#!");

        // dotnet watch App.txt
        var options1 = ParseOptions([filePath]);
        var result1 = Program.GetMainProjectOptions(options1, tempDir, _testLogger);

        Assert.IsNotNull(result1);
        Assert.AreEqual(filePath, result1.Representation.EntryPointFilePath);

        // dotnet watch -bl -e X=1 App.txt
        var options2 = ParseOptions(["-bl", "-e", "X=1", filePath]);
        var result2 = Program.GetMainProjectOptions(options2, tempDir, _testLogger);

        Assert.IsNotNull(result2);
        Assert.AreEqual(filePath, result2.Representation.EntryPointFilePath);
    }

    [TestMethod]
    public void RelativeProjectPath()
    {
        var tempDir = CreateTempDirectory();
        var subDir = Path.Combine(tempDir, "subdir");
        Directory.CreateDirectory(subDir);
        var projectPath = Path.Combine(subDir, "Test.csproj");
        File.WriteAllText(projectPath, "<Project></Project>");

        var options = ParseOptions(["--project", "subdir/Test.csproj"]);
        var result = Program.GetMainProjectOptions(options, tempDir, _testLogger);

        Assert.IsNotNull(result);
        Assert.AreEqual(projectPath, result.Representation.PhysicalPath);
    }

    [TestMethod]
    public void RelativeFilePath()
    {
        var tempDir = CreateTempDirectory();
        var csFilePath = Path.Combine(tempDir, "Script.cs");
        File.WriteAllText(csFilePath, "Console.WriteLine(\"Hello\");");

        var options = ParseOptions(["--file", "Script.cs"]);
        var result = Program.GetMainProjectOptions(options, tempDir, _testLogger);

        Assert.IsNotNull(result);
        Assert.AreEqual(csFilePath, result.Representation.EntryPointFilePath);
    }

    [TestMethod]
    public void FilePathOptionTakesPrecedenceOverProjectPath()
    {
        var tempDir = CreateTempDirectory();
        var projectPath = Path.Combine(tempDir, "Test.csproj");
        File.WriteAllText(projectPath, "<Project></Project>");
        var csFilePath = Path.Combine(tempDir, "Script.cs");
        File.WriteAllText(csFilePath, "Console.WriteLine(\"Hello\");");

        var options = ParseOptions(["--file", csFilePath]);
        var result = Program.GetMainProjectOptions(options, tempDir, _testLogger);

        Assert.IsNotNull(result);
        Assert.AreEqual(csFilePath, result.Representation.EntryPointFilePath);
        Assert.IsNull(result.Representation.PhysicalPath);
    }
}
