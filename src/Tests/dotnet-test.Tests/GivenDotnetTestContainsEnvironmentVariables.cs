// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using System.IO;
using System;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestContainsEnvironmentVariables : SdkTest
    {
        private const string TestAppName = "VSTestEnvironmentVariables";
        private const string EnvironmentOption = "--environment";
        private const string EnvironmentVariable1 = "__DOTNET_TEST_ENVIRONMENT_VARIABLE_EMPTY";
        private const string EnvironmentVariable2 = "__DOTNET_TEST_ENVIRONMENT_VARIABLE_1=VALUE1";
        private const string EnvironmentVariable3 = "__DOTNET_TEST_ENVIRONMENT_VARIABLE_2=VALUE WITH SPACE";

        public GivenDotnetTestContainsEnvironmentVariables(ITestOutputHelper log) : base(log)
        {
        }

        private readonly string[] ConsoleLoggerOutputDetailed = new[] { "--logger", "console;verbosity=detailed" };


        private readonly string[] EnvironmentVariables = new[] {
            EnvironmentOption, EnvironmentVariable1,
            EnvironmentOption, EnvironmentVariable2,
            EnvironmentOption, EnvironmentVariable3,
        };

        [Fact]
        public void ItPassesEnvironmentVariablesFromCommandLineParametersWhenRunningViaCsproj()
        {
            var testAsset = _testAssetsManager.CopyTestAsset(TestAppName)
                .WithSource()
                .WithVersionVariables();

            var testRoot = testAsset.Path;

            CommandResult result = new DotnetTestCommand(Log, EnvironmentVariables)
                                        .WithWorkingDirectory(testRoot)
                                        .Execute(ConsoleLoggerOutputDetailed);

            result.StdOut
                  .Should().Contain(EnvironmentVariable1)
                  .And.Contain(EnvironmentVariable2)
                  .And.Contain(EnvironmentVariable3);

            if (!TestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Total tests: 1")
                    .And.Contain("Passed: 1")
                    .And.Contain("Passed TestEnvironmentVariables");
            }

            result.ExitCode.Should().Be(0);
        }

        [Fact]
        public void ItPassesEnvironmentVariablesFromCommandLineParametersWhenRunningViaDll()
        {
            var testAsset = _testAssetsManager.CopyTestAsset(TestAppName)
                .WithSource()
                .WithVersionVariables();

            var testRoot = testAsset.Path;

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            new BuildCommand(testAsset)
                .Execute()
                .Should().Pass();

            var outputDll = Path.Combine(testRoot, "bin", configuration, "netcoreapp3.1", $"{TestAppName}.dll");

            var result = new DotnetTestCommand(Log, EnvironmentVariables)
                .Execute(outputDll, $"{ConsoleLoggerOutputDetailed[0]}:{ConsoleLoggerOutputDetailed[1]}");

            result.StartInfo.Arguments
                .Should().Contain($"{EnvironmentOption} {EnvironmentVariable1}")
                .And.Contain($"{EnvironmentOption} {EnvironmentVariable2}")
                .And.Contain($"{EnvironmentOption} \"{EnvironmentVariable3}\"");

            result.StdOut
                  .Should().Contain(EnvironmentVariable1)
                  .And.Contain(EnvironmentVariable2)
                  .And.Contain(EnvironmentVariable3);

            if (!TestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Total tests: 1")
                    .And.Contain("Passed: 1")
                    .And.Contain("Passed TestEnvironmentVariables");
            }

            result.ExitCode.Should().Be(0);
        }
    }
}
