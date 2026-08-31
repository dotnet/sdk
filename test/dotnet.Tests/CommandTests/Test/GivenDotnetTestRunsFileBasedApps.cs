// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Build.Logging.StructuredLogger;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;
using ExitCodes = Microsoft.NET.TestFramework.ExitCode;

namespace Microsoft.DotNet.Cli.Test.Tests;

[TestClass]
public class GivenDotnetTestRunsFileBasedApps : SdkTest
{
    [TestMethod]
    [CombinatorialData]
    public void RunPassingTest([CombinatorialValues(false, true)] bool useProjectOption)
    {
        var testDirectory = CreateTestDirectory(
            """
            [TestClass]
            public class FileBasedTests
            {
                [TestMethod]
                public void Passes()
                {
                }
            }
            """);

        string[] arguments = useProjectOption
            ? ["--project", "PassingTests.cs", "--no-progress"]
            : ["PassingTests.cs", "--no-progress"];

        CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testDirectory.Path)
            .Execute(arguments);

        result.ExitCode.Should().Be(ExitCodes.Success);
        if (!SdkTestContext.IsLocalized())
        {
            result.StdOut
                .Should().Contain("Test run summary: Passed!")
                .And.Contain("total: 1")
                .And.Contain("succeeded: 1");
        }
    }

    [TestMethod]
    public void RunFailingTest()
    {
        var testDirectory = CreateTestDirectory(
            """
            [TestClass]
            public class FileBasedTests
            {
                [TestMethod]
                public void Fails()
                {
                    Assert.Fail("Expected failure");
                }
            }
            """);

        CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testDirectory.Path)
            .Execute("PassingTests.cs", "--no-progress");

        result.ExitCode.Should().Be(ExitCodes.AtLeastOneTestFailed);
        result.StdOut.Should().Contain("Expected failure");
    }

    [TestMethod]
    public void ListTests()
    {
        var testDirectory = CreateTestDirectory(
            """
            [TestClass]
            public class FileBasedTests
            {
                [TestMethod]
                public void IsDiscovered()
                {
                }
            }
            """);

        CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testDirectory.Path)
            .Execute("PassingTests.cs", "--list-tests", "--no-progress");

        result.ExitCode.Should().Be(ExitCodes.Success);
        result.StdOut.Should().Contain("IsDiscovered");
    }

    [TestMethod]
    public void RunWithNoBuild()
    {
        var testDirectory = CreateTestDirectory(
            """
            [TestClass]
            public class FileBasedTests
            {
                [TestMethod]
                public void Passes()
                {
                }
            }
            """);
        var command = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testDirectory.Path);

        command.Execute("PassingTests.cs", "--no-progress")
            .ExitCode.Should().Be(ExitCodes.Success);

        command.Execute("PassingTests.cs", "--no-build", "--no-progress")
            .ExitCode.Should().Be(ExitCodes.Success);
    }

    [TestMethod]
    public void RunWithNoRestore()
    {
        var testDirectory = CreateTestDirectory(
            """
            [TestClass]
            public class FileBasedTests
            {
                [TestMethod]
                public void Passes()
                {
                }
            }
            """);
        var command = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testDirectory.Path);

        command.Execute("PassingTests.cs", "--no-progress")
            .ExitCode.Should().Be(ExitCodes.Success);

        command.Execute("PassingTests.cs", "--no-restore", "--no-progress")
            .ExitCode.Should().Be(ExitCodes.Success);
    }

    [TestMethod]
    public void PropagateBuildOptions()
    {
        var testDirectory = CreateTestDirectory(
            """
            [TestClass]
            public class FileBasedTests
            {
                [TestMethod]
                public void ReceivesBuildOptions()
                {
            #if !CUSTOM_DEFINE
                    Assert.Fail("CUSTOM_DEFINE was not set");
            #endif
                }
            }
            """);

        CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testDirectory.Path)
            .Execute("PassingTests.cs", "--configuration", "Release", "-p:DefineConstants=CUSTOM_DEFINE", "--no-progress");

        result.ExitCode.Should().Be(ExitCodes.Success);
        result.StdOut.Should().Contain($"{Path.DirectorySeparatorChar}release{Path.DirectorySeparatorChar}");
    }

    [TestMethod]
    public void ProduceBinaryLogs()
    {
        var testDirectory = CreateTestDirectory(
            """
            [TestClass]
            public class FileBasedTests
            {
                [TestMethod]
                public void Passes()
                {
                }
            }
            """);

        new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testDirectory.Path)
            .Execute("PassingTests.cs", "-bl", "--no-progress")
            .ExitCode.Should().Be(ExitCodes.Success);

        foreach (string fileName in new[] { "msbuild.binlog", "msbuild-dotnet-test.binlog" })
        {
            string binlogPath = Path.Combine(testDirectory.Path, fileName);
            File.Exists(binlogPath).Should().BeTrue();
            BinaryLog.ReadBuild(binlogPath).Should().NotBeNull();
        }
    }

    [TestMethod]
    public void RunMultipleTargetFrameworks()
    {
        var testDirectory = CreateTestDirectory(
            $$"""
            #:property TargetFramework=
            #:property TargetFrameworks={{ToolsetInfo.CurrentTargetFramework}};{{DotnetVersionHelper.GetPreviousDotnetVersion()}}
            #:property PublishAot=false

            [TestClass]
            public class FileBasedTests
            {
                [TestMethod]
                public void Passes()
                {
                }
            }
            """);

        CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testDirectory.Path)
            .Execute("PassingTests.cs", "--no-progress");

        result.ExitCode.Should().Be(ExitCodes.Success);
        result.StdOut.Should().Contain("total: 2");

        result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testDirectory.Path)
            .Execute("PassingTests.cs", "--no-build", "--no-progress");

        result.ExitCode.Should().Be(ExitCodes.Success);
        result.StdOut.Should().Contain("total: 2");
    }

    [TestMethod]
    [DataRow("--device")]
    [DataRow("--list-devices")]
    public void DeviceOptionsAreRejected(string option)
    {
        var testDirectory = CreateTestDirectory(
            """
            [TestClass]
            public class FileBasedTests
            {
                [TestMethod]
                public void Passes()
                {
                }
            }
            """);

        string[] arguments = option == "--device"
            ? ["PassingTests.cs", option, "local"]
            : ["PassingTests.cs", option];

        CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testDirectory.Path)
            .Execute(arguments);

        result.ExitCode.Should().NotBe(ExitCodes.Success);
        if (!SdkTestContext.IsLocalized())
        {
            result.StdErr.Should().Contain("not supported for C# file-based apps");
        }
    }

    [TestMethod]
    public void FilePassedAsNonFirstPositionalArgumentIsRejected()
    {
        var testDirectory = CreateTestDirectory(
            """
            [TestClass]
            public class FileBasedTests
            {
                [TestMethod]
                public void Passes()
                {
                }
            }
            """);

        CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testDirectory.Path)
            .Execute("--unknown-option", "PassingTests.cs");

        result.ExitCode.Should().NotBe(ExitCodes.Success);
        if (!SdkTestContext.IsLocalized())
        {
            result.StdErr.Should().Contain("Specifying a project for 'dotnet test' should be via '--project'.");
        }
    }

    [TestMethod]
    public void FileAfterDoubleDashIsNotUsedAsTheTestInput()
    {
        var testDirectory = CreateTestDirectory(
            """
            [TestClass]
            public class FileBasedTests
            {
                [TestMethod]
                public void Passes()
                {
                }
            }
            """);

        CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testDirectory.Path)
            .Execute("--", "PassingTests.cs");

        result.ExitCode.Should().NotBe(ExitCodes.Success);
        result.StdOut.Should().NotContain("Test run summary: Passed!");
    }

    [TestMethod]
    public void RepeatedFileAfterDoubleDashDoesNotHideTheTestInput()
    {
        var testDirectory = CreateTestDirectory(
            """
            [TestClass]
            public class FileBasedTests
            {
                [TestMethod]
                public void Passes()
                {
                }
            }
            """);

        CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testDirectory.Path)
            .Execute("PassingTests.cs", "--", "PassingTests.cs");

        result.StdOut.Should().Contain("PassingTests.cs ->");
    }

    [TestMethod]
    public void MissingFileAsNonFirstArgumentIsNotTreatedAsTheTestInput()
    {
        var testDirectory = CreateTestDirectory(string.Empty);

        CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testDirectory.Path)
            .Execute("--unknown-option", "MissingTests.cs");

        if (!SdkTestContext.IsLocalized())
        {
            result.StdErr.Should().NotContain("Specifying a project for 'dotnet test' should be via '--project'.");
        }
    }

    [TestMethod]
    public void MissingFileFailsInsteadOfFallingBackToDirectoryDiscovery()
    {
        var testDirectory = CreateTestDirectory(string.Empty);
        File.Delete(Path.Combine(testDirectory.Path, "PassingTests.cs"));

        CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testDirectory.Path)
            .Execute("MissingTests.cs");

        result.ExitCode.Should().NotBe(ExitCodes.Success);
        if (!SdkTestContext.IsLocalized())
        {
            result.StdErr.Should().Contain($"The provided file path does not exist: {Path.Combine(testDirectory.Path, "MissingTests.cs")}");
        }
    }

    private TestDirectory CreateTestDirectory(string testSource)
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory(identifier: Guid.NewGuid().ToString());
        string? versionsPropsPath = PathUtility.FindFileInParentDirectories(
            SdkTestContext.Current.TestExecutionDirectory,
            $"eng{Path.DirectorySeparatorChar}Version.Details.props");
        versionsPropsPath.Should().NotBeNull();

        string mstestVersion = System.Xml.Linq.XDocument.Load(versionsPropsPath!)
            .Descendants("MSTestPackageVersion")
            .Single()
            .Value;

        File.WriteAllText(
            Path.Combine(testDirectory.Path, "PassingTests.cs"),
            $"#:sdk MSTest.Sdk@{mstestVersion}{Environment.NewLine}{Environment.NewLine}{testSource}");
        File.WriteAllText(
            Path.Combine(testDirectory.Path, "global.json"),
            """
            {
              "test": {
                "runner": "Microsoft.Testing.Platform"
              }
            }
            """);

        return testDirectory;
    }
}
