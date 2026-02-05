// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;

namespace dotnet.Tests
{
    public class FrameworkOptionTests : SdkTest
    {
        public FrameworkOptionTests(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("build", true)]
        [InlineData("clean", true)]
        [InlineData("publish", true)]
        [InlineData("test", true)]
        public void FrameworkOptionGeneratesWarningsWithSolutionFiles(string command, bool shouldWarn)
        {
            TestFrameworkWithSolution(command, useOption: true, shouldWarn: shouldWarn);
        }

        [Theory]
        [InlineData("build")]
        [InlineData("clean")]
        [InlineData("publish")]
        [InlineData("test")]
        public void TargetFrameworkPropertyDoesNotGenerateWarningsWithSolutionFiles(string command)
        {
            TestFrameworkWithSolution(command, useOption: false, shouldWarn: false);
        }

        void TestFrameworkWithSolution(string command, bool useOption, bool shouldWarn, [CallerMemberName] string callingMethod = "")
        {
            var testProject = new TestProject()
            {
                IsExe = true,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, callingMethod, identifier: command);

            var slnDirectory = testAsset.TestRoot;

            Log.WriteLine($"Test root: {slnDirectory}");

            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(slnDirectory)
                .Execute("sln")
                .Should().Pass();

            new DotnetCommand(Log)
                .WithWorkingDirectory(slnDirectory)
                .Execute("sln", "add", testProject.Name)
                .Should().Pass();

            Microsoft.DotNet.Cli.Utils.CommandResult commandResult;
            if (useOption)
            {
                commandResult = new DotnetCommand(Log)
                    .WithWorkingDirectory(slnDirectory)
                    .Execute(command, "--framework", ToolsetInfo.CurrentTargetFramework);
            }
            else
            {
                commandResult = new DotnetCommand(Log)
                    .WithWorkingDirectory(slnDirectory)
                    .Execute(command, $"--property:TargetFramework={ToolsetInfo.CurrentTargetFramework}");
            }
            commandResult.Should().Pass();
            if (shouldWarn)
            {
                commandResult.Should().HaveStdOutContaining("NETSDK1235");
            }
            else
            {
                commandResult.Should().NotHaveStdOutContaining("NETSDK1235");
            }
        }
    }
}
