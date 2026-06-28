// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;

namespace dotnet.Tests
{
    [TestClass]
    public class OutputPathOptionTests : SdkTest
    {
        public OutputPathOptionTests()
        {
        }

        [TestMethod]
        [DataRow("build", true)]
        [DataRow("clean", true)]
        [DataRow("pack", false)]
        [DataRow("publish", true)]
        [DataRow("test", true)]
        public void OutputOptionGeneratesWarningsWithSolutionFiles(string command, bool shouldWarn)
        {
            TestOutputWithSolution(command, useOption: true, shouldWarn: shouldWarn);
        }

        [TestMethod]
        [DataRow("build")]
        [DataRow("clean")]
        [DataRow("pack")]
        [DataRow("publish")]
        [DataRow("test")]
        public void OutputPathPropertyDoesNotGenerateWarningsWithSolutionFiles(string command)
        {
            TestOutputWithSolution(command, useOption: false, shouldWarn: false);
        }

        void TestOutputWithSolution(string command, bool useOption, bool shouldWarn, [CallerMemberName] string callingMethod = "")
        {
            var testProject = new TestProject()
            {
                IsExe = true,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework
            };

            var testAsset = TestAssetsManager.CreateTestProject(testProject, callingMethod, identifier: command);

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

            string outputDirectory = Path.Combine(slnDirectory, "bin");
            Microsoft.DotNet.Cli.Utils.CommandResult commandResult;
            if (useOption)
            {
                commandResult = new DotnetCommand(Log)
                    .WithWorkingDirectory(slnDirectory)
                    .Execute(command, "--output", outputDirectory);
            }
            else
            {
                commandResult = new DotnetCommand(Log)
                    .WithWorkingDirectory(slnDirectory)
                    .Execute(command, $"--property:OutputPath={outputDirectory}");
            }
            commandResult.Should().Pass();
            if (shouldWarn)
            {
                commandResult.Should().HaveStdOutContaining("NETSDK1194");
            }
            else
            {
                commandResult.Should().NotHaveStdOutContaining("NETSDK1194");
            }
        }
    }
}
