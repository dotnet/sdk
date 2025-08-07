// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Test.Utilities;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class TestCommandValidationTests : SdkTest
    {
        public TestCommandValidationTests(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("MySolution.sln", "Solution file 'MySolution.sln' was provided as a positional argument.")]
        [InlineData("MyProject.csproj", "Project file 'MyProject.csproj' was provided as a positional argument.")]
        [InlineData("MyProject.vbproj", "Project file 'MyProject.vbproj' was provided as a positional argument.")]
        [InlineData("MyProject.fsproj", "Project file 'MyProject.fsproj' was provided as a positional argument.")]
        public void TestCommandShouldValidateFileArgumentsAndProvideHelpfulMessages(string filename, string expectedErrorStart)
        {
            var testDir = _testAssetsManager.CreateTestDirectory();
            
            // Create the test file
            var testFilePath = Path.Combine(testDir.Path, filename);
            File.WriteAllText(testFilePath, "dummy content");

            var result = new DotnetTestCommand(Log, disableNewOutput: true)
                .WithWorkingDirectory(testDir.Path)
                .Execute(filename);

            result.ExitCode.Should().Be(1);
            result.StdErr.Should().Contain(expectedErrorStart);
            result.StdErr.Should().Contain("Testing Platform");
            result.StdErr.Should().Contain("dotnet.config");
        }

        [Fact]
        public void TestCommandShouldValidateDirectoryArgumentAndProvideHelpfulMessage()
        {
            var testDir = _testAssetsManager.CreateTestDirectory();
            var subDir = Path.Combine(testDir.Path, "test_directory");
            Directory.CreateDirectory(subDir);

            var result = new DotnetTestCommand(Log, disableNewOutput: true)
                .WithWorkingDirectory(testDir.Path)
                .Execute("test_directory");

            result.ExitCode.Should().Be(1);
            result.StdErr.Should().Contain("Directory 'test_directory' was provided as a positional argument.");
            result.StdErr.Should().Contain("--directory test_directory");
            result.StdErr.Should().Contain("Testing Platform");
        }

        [Fact]
        public void TestCommandShouldValidateDllArgumentAndProvideHelpfulMessage()
        {
            var testDir = _testAssetsManager.CreateTestDirectory();
            
            // Create a dummy dll file
            var dllPath = Path.Combine(testDir.Path, "test.dll");
            File.WriteAllText(dllPath, "dummy dll content");

            var result = new DotnetTestCommand(Log, disableNewOutput: true)
                .WithWorkingDirectory(testDir.Path)
                .Execute("test.dll");

            result.ExitCode.Should().Be(1);
            result.StdErr.Should().Contain("Test assembly 'test.dll' was provided as a positional argument.");
            result.StdErr.Should().Contain("--test-modules test.dll");
            result.StdErr.Should().Contain("Testing Platform");
        }

        [Fact]
        public void TestCommandShouldAllowNormalOptionsWithoutValidation()
        {
            var testDir = _testAssetsManager.CreateTestDirectory();

            // Test that normal options like --help still work without triggering validation
            var result = new DotnetTestCommand(Log, disableNewOutput: true)
                .Execute("--help");

            result.ExitCode.Should().Be(0);
            result.StdOut.Should().Contain("Usage:");
            result.StdOut.Should().Contain("dotnet test");
        }

        [Fact]
        public void TestCommandShouldNotValidateNonExistentFiles()
        {
            var testDir = _testAssetsManager.CreateTestDirectory();

            // Test that non-existent files with project extensions don't trigger validation
            var result = new DotnetTestCommand(Log, disableNewOutput: true)
                .WithWorkingDirectory(testDir.Path)
                .Execute("NonExistent.csproj");

            // This should pass through to MSBuild and give a different error, not our validation error
            result.ExitCode.Should().Be(1);
            result.StdErr.Should().NotContain("was provided as a positional argument");
            result.StdErr.Should().NotContain("Testing Platform");
        }

        [Theory]
        [InlineData("MySolution.sln", "Solution file 'MySolution.sln' was provided as a positional argument.", "--solution MySolution.sln")]
        [InlineData("MyProject.csproj", "Project file 'MyProject.csproj' was provided as a positional argument.", "--project MyProject.csproj")]
        [InlineData("MyProject.vbproj", "Project file 'MyProject.vbproj' was provided as a positional argument.", "--project MyProject.vbproj")]
        [InlineData("MyProject.fsproj", "Project file 'MyProject.fsproj' was provided as a positional argument.", "--project MyProject.fsproj")]
        public void TestCommandWithMTPShouldValidateFileArgumentsAndProvideDirectGuidance(string filename, string expectedErrorStart, string expectedSuggestion)
        {
            var testDir = _testAssetsManager.CreateTestDirectory();
            
            // Create dotnet.config to enable MTP
            var configPath = Path.Combine(testDir.Path, "dotnet.config");
            File.WriteAllText(configPath, "[dotnet.test.runner]\nname = Microsoft.Testing.Platform");
            
            // Create the test file
            var testFilePath = Path.Combine(testDir.Path, filename);
            File.WriteAllText(testFilePath, "dummy content");

            var result = new DotnetTestCommand(Log, disableNewOutput: true)
                .WithWorkingDirectory(testDir.Path)
                .Execute(filename);

            result.ExitCode.Should().Be(1);
            result.StdErr.Should().Contain(expectedErrorStart);
            result.StdErr.Should().Contain(expectedSuggestion);
            result.StdErr.Should().NotContain("dotnet.config"); // MTP is already enabled, so no need to suggest enabling it
        }

        [Fact]
        public void TestCommandWithMTPShouldValidateDirectoryArgumentAndProvideDirectGuidance()
        {
            var testDir = _testAssetsManager.CreateTestDirectory();
            
            // Create dotnet.config to enable MTP
            var configPath = Path.Combine(testDir.Path, "dotnet.config");
            File.WriteAllText(configPath, "[dotnet.test.runner]\nname = Microsoft.Testing.Platform");
            
            var subDir = Path.Combine(testDir.Path, "test_directory");
            Directory.CreateDirectory(subDir);

            var result = new DotnetTestCommand(Log, disableNewOutput: true)
                .WithWorkingDirectory(testDir.Path)
                .Execute("test_directory");

            result.ExitCode.Should().Be(1);
            result.StdErr.Should().Contain("Directory 'test_directory' was provided as a positional argument.");
            result.StdErr.Should().Contain("--directory test_directory");
            result.StdErr.Should().NotContain("dotnet.config"); // MTP is already enabled
        }

        [Fact]
        public void TestCommandWithMTPShouldValidateDllArgumentAndProvideDirectGuidance()
        {
            var testDir = _testAssetsManager.CreateTestDirectory();
            
            // Create dotnet.config to enable MTP
            var configPath = Path.Combine(testDir.Path, "dotnet.config");
            File.WriteAllText(configPath, "[dotnet.test.runner]\nname = Microsoft.Testing.Platform");
            
            // Create a dummy dll file
            var dllPath = Path.Combine(testDir.Path, "test.dll");
            File.WriteAllText(dllPath, "dummy dll content");

            var result = new DotnetTestCommand(Log, disableNewOutput: true)
                .WithWorkingDirectory(testDir.Path)
                .Execute("test.dll");

            result.ExitCode.Should().Be(1);
            result.StdErr.Should().Contain("Test assembly 'test.dll' was provided as a positional argument.");
            result.StdErr.Should().Contain("--test-modules test.dll");
            result.StdErr.Should().NotContain("dotnet.config"); // MTP is already enabled
        }
    }
}