// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests;

public class EnvironmentVariablesDirectiveTests : SdkTest
{
    public EnvironmentVariablesDirectiveTests(ITestOutputHelper log) : base(log)
    {
    }

    [Fact]
    public void CanApplyEnvVarToInvocation()
    {
        var envVarName = "mything";
        var envVarValue = "foo";
        var parseResult = Parser.Instance.Parse([$"[env:{envVarName}={envVarValue}]", "--info"]);
        var result = parseResult.Invoke();

        System.Environment.GetEnvironmentVariable(envVarName).Should().Be(envVarValue);
    }
}
