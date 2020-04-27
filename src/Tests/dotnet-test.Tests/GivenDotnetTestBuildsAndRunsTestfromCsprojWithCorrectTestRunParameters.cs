// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestBuildsAndRunsTestfromCsprojWithCorrectTestRunParameters : SdkTest
    {
        public GivenDotnetTestBuildsAndRunsTestfromCsprojWithCorrectTestRunParameters(ITestOutputHelper log) : base(log)
        {
        }

        private readonly string[] ConsoleLoggerOutputNormal = new[] { "--logger", "console;verbosity=normal" };

        [Fact]
        public void MSTestSingleTFM()
        {
            var testProjectDirectory = this.CopyAndRestoreVSTestDotNetCoreTestApp("1");

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                                        .WithWorkingDirectory(testProjectDirectory)
                                        .Execute(ConsoleLoggerOutputNormal.Concat(new[] {
                                            "--",
                                            "TestRunParameters.Parameter(name=\"myParam\",",
                                            "value=\"value\")",
                                            "TestRunParameters.Parameter(name=\"myParam2\",",
                                            "value=\"myValue with space\")"
                                        }));

            // Verify
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().NotMatch("The test run parameter argument '*' is invalid.");
                result.StdOut.Should().Contain("Total tests: 1");
                result.StdOut.Should().Contain("Passed: 1");
                result.StdOut.Should().Contain("Failed: 0");
                result.StdOut.Should().Contain("\u221a VSTestTestRunParameters");
            }

            result.ExitCode.Should().Be(0);
        }

        // dotnet test + dll
        //[Fact]
        //public void TestsFromAGivenContainerShouldRunWithExpectedOutput()
        //{
        //    var testAppName = "VSTestCore";
        //    var testRoot = _testAssetsManager.CopyTestAsset(testAppName)
        //        .WithSource()
        //        .WithVersionVariables()
        //        .Path;

        //    var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

        //    new BuildCommand(Log, testRoot)
        //        .Execute()
        //        .Should().Pass();

        //    var outputDll = Path.Combine(testRoot, "bin", configuration, "netcoreapp3.0", $"{testAppName}.dll");

        //    // Call vstest
        //    var result = new DotnetVSTestCommand(Log)
        //        .Execute(outputDll, "--logger:console;verbosity=normal");
        //    if (!TestContext.IsLocalized())
        //    {
        //        result.StdOut
        //            .Should().Contain("Total tests: 2")
        //            .And.Contain("Passed: 1")
        //            .And.Contain("Failed: 1")
        //            .And.Contain("\u221a VSTestPassTest")
        //            .And.Contain("X VSTestFailTest");
        //    }

        //    result.ExitCode.Should().Be(1);
        //}

        // vstest
        //[Fact]
        //public void TestsFromAGivenContainerShouldRunWithExpectedOutput()
        //{
        //    var testAppName = "VSTestCore";
        //    var testRoot = _testAssetsManager.CopyTestAsset(testAppName)
        //        .WithSource()
        //        .WithVersionVariables()
        //        .Path;

        //    var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

        //    new BuildCommand(Log, testRoot)
        //        .Execute()
        //        .Should().Pass();

        //    var outputDll = Path.Combine(testRoot, "bin", configuration, "netcoreapp3.0", $"{testAppName}.dll");

        //    // Call vstest
        //    var result = new DotnetVSTestCommand(Log)
        //        .Execute(outputDll, "--logger:console;verbosity=normal");
        //    if (!TestContext.IsLocalized())
        //    {
        //        result.StdOut
        //            .Should().Contain("Total tests: 2")
        //            .And.Contain("Passed: 1")
        //            .And.Contain("Failed: 1")
        //            .And.Contain("\u221a VSTestPassTest")
        //            .And.Contain("X VSTestFailTest");
        //    }

        //    result.ExitCode.Should().Be(1);
        //}

        private string CopyAndRestoreVSTestDotNetCoreTestApp([CallerMemberName] string callingMethod = "")
        {
            // Copy VSTestCore project in output directory of project dotnet-vstest.Tests
            string testAppName = "VSTestTestRunParameters";

            var testInstance = _testAssetsManager.CopyTestAsset(testAppName, callingMethod: callingMethod)
                            .WithSource()
                            .WithVersionVariables();

            var testProjectDirectory = testInstance.Path;

            // Restore project VSTestCore
            new RestoreCommand(Log, testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            return testProjectDirectory;
        }
    }
}
