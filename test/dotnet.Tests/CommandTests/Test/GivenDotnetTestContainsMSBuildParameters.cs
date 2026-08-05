// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    [TestClass]
    public class GivenDotnetTestContainsMSBuildParameters : SdkTest
    {
        private const string TestAppName = "VSTestMSBuildParameters";
        private const string MSBuildParameter = "/p:Version=1.2.3";

        public GivenDotnetTestContainsMSBuildParameters()
        {
        }

        [DataRow($"{TestAppName}.csproj")]
        [DataRow(null)]
        [TestMethod]
        public void ItPassesEnvironmentVariablesFromCommandLineParametersWhenRunningViaCsproj(string projectName)
        {
            var testAsset = TestAssetsManager.CopyTestAsset(TestAppName)
                .WithSource()
                .WithVersionVariables();

            var testRoot = testAsset.Path;

            CommandResult result = (projectName is null ? new DotnetTestCommand(Log, disableNewOutput: true) : new DotnetTestCommand(Log, disableNewOutput: true, projectName))
                                    .WithWorkingDirectory(testRoot)
                                    .Execute("--logger", "console;verbosity=detailed", MSBuildParameter);

            if (!SdkTestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Total tests: 1")
                    .And.Contain("Passed: 1")
                    .And.Contain("Passed TestMSBuildParameters");
            }

            result.ExitCode.Should().Be(0);
        }
    }
}
