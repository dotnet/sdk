// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Test.Utilities;

namespace Microsoft.DotNet.Cli.Test.Tests;

[TestClass]
public class CultureAwareTestProject : SdkTest
{
    private const string TestAppName = "TestAppSimple";

    public CultureAwareTestProject()
    {
    }

    [DataRow("en-US")]
    [DataRow("de-DE")]
    [TestMethod]
    public void CanRunTestsAgainstProjectInLocale(string locale)
    {
        var testAsset = TestAssetsManager.CopyTestAsset(TestAppName)
                .WithSource()
                .WithVersionVariables();

        var command = new DotnetTestCommand(Log, disableNewOutput: true).WithWorkingDirectory(testAsset.Path).WithCulture(locale);
        var result = command.Execute();

        result.ExitCode.Should().Be(0);
    }
}
