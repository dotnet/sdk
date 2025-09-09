// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class TestCommandValidationTests : SdkTest
    {
        public TestCommandValidationTests(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("MySolution.sln", "Specifying a solution for 'dotnet test' should be via '--solution'.")]
        [InlineData("MyProject.csproj", "Specifying a project for 'dotnet test' should be via '--project'.")]
        [InlineData("MyProject.vbproj", "Specifying a project for 'dotnet test' should be via '--project'.")]
        [InlineData("MyProject.fsproj", "Specifying a project for 'dotnet test' should be via '--project'.")]
        public void TestCommandShouldValidateFileArgumentsAndProvideHelpfulMessages(string filename, string expectedErrorStart)
        {
            var testDir = _testAssetsManager.CreateTestDirectory();

            // Create the test file
            var testFilePath = Path.Combine(testDir.Path, filename);
            File.WriteAllText(testFilePath, "dummy content");
            File.WriteAllText(Path.Combine(testDir.Path, "dotnet.config"),
                """
                [dotnet.test.runner]
                name = Microsoft.Testing.Platform
                """);

            var result = new DotnetTestCommand(Log, disableNewOutput: false)
                .WithWorkingDirectory(testDir.Path)
                .Execute(filename);

            result.ExitCode.Should().NotBe(0);
            if (!TestContext.IsLocalized())
            {
                result.StdErr.Should().Contain(expectedErrorStart);
            }
        }

        [Fact]
        public void TestCommandShouldValidateDirectoryArgumentAndProvideHelpfulMessage()
        {
            var testDir = _testAssetsManager.CreateTestDirectory();
            var subDir = Path.Combine(testDir.Path, "test_directory");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(testDir.Path, "dotnet.config"),
                """
                [dotnet.test.runner]
                name = Microsoft.Testing.Platform
                """);

            var result = new DotnetTestCommand(Log, disableNewOutput: false)
                .WithWorkingDirectory(testDir.Path)
                .Execute("test_directory");

            result.ExitCode.Should().NotBe(0);
            if (!TestContext.IsLocalized())
            {
                result.StdErr.Should().Contain("Specifying a directory for 'dotnet test' should be via '--project' or '--solution'.");
            }
        }

        [Fact]
        public void TestCommandShouldValidateDllArgumentAndProvideHelpfulMessage()
        {
            var testDir = _testAssetsManager.CreateTestDirectory();

            // Create a dummy dll file
            var dllPath = Path.Combine(testDir.Path, "test.dll");
            File.WriteAllText(dllPath, "dummy dll content");
            File.WriteAllText(Path.Combine(testDir.Path, "dotnet.config"),
                """
                [dotnet.test.runner]
                name = Microsoft.Testing.Platform
                """);

            var result = new DotnetTestCommand(Log, disableNewOutput: false)
                .WithWorkingDirectory(testDir.Path)
                .Execute("test.dll");

            result.ExitCode.Should().NotBe(0);
            if (!TestContext.IsLocalized())
            {
                result.StdErr.Should().Contain("Specifying dlls or executables for 'dotnet test' should be via '--test-modules'.");
            }
        }
    }
}
