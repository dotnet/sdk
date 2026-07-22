// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;
using ExitCodes = Microsoft.NET.TestFramework.ExitCode;

namespace Microsoft.DotNet.Cli.Test.Tests;

[TestClass]
public class GivenDotnetTestsRunsWithDifferentCultures : SdkTest
{
    public GivenDotnetTestsRunsWithDifferentCultures()
    {
    }

    [DataRow("en-US")]
    [DataRow("de-DE")]
    [TestMethod]
    public void CanRunTestsAgainstProjectInLocale(string locale)
    {
        TestAsset testInstance = TestAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString()).WithSource();

        CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                .WithWorkingDirectory(testInstance.Path)
                                .WithCulture(locale)
                                .Execute();

        result.ExitCode.Should().Be(ExitCodes.Success);
    }
}
