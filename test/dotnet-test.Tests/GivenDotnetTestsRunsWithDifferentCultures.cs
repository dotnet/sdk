// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;

namespace Microsoft.DotNet.Cli.Test.Tests;

public class GivenDotnetTestsRunsWithDifferentCultures : SdkTest
{
    public GivenDotnetTestsRunsWithDifferentCultures(ITestOutputHelper log) : base(log)
    {
    }

    [InlineData("en-US")]
    [InlineData("de-DE")]
    [Theory]
    public void CanRunTestsAgainstProjectInLocale(string locale)
    {
        TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString()).WithSource();

        CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                .WithWorkingDirectory(testInstance.Path)
                                .WithCulture(locale)
                                .Execute();

        result.ExitCode.Should().Be(0);
    }
}
