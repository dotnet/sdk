// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test;

namespace dotnet.Tests.CommandTests.Test;

public class AzureDevOpsUtilitiesTests
{
    [Theory]
    [InlineData("##vso[task.logissue type=error]boom", true)]
    [InlineData("##vso[task.complete result=Failed;]", true)]
    [InlineData("##[group]My group", true)]
    [InlineData("##[endgroup]", true)]
    [InlineData("##[warning]careful", true)]
    [InlineData("  ##vso[task.logissue type=error]leading whitespace", true)]
    [InlineData("\t##[group]tab indented", true)]
    [InlineData("Just a normal line", false)]
    [InlineData("#vso[not a command]", false)]
    [InlineData("## not a command", false)]
    [InlineData("text ##vso[task.logissue] in the middle", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsAzureDevOpsLoggingCommand_RecognizesPipelineCommands(string? line, bool expected)
        => AzureDevOpsUtilities.IsAzureDevOpsLoggingCommand(line).Should().Be(expected);

    [Fact]
    public void IsAzureDevOpsEnvironment_WhenTfBuildTrue_ReturnsTrue()
        => AzureDevOpsUtilities.IsAzureDevOpsEnvironment(Env(("TF_BUILD", "true"))).Should().BeTrue();

    [Fact]
    public void IsAzureDevOpsEnvironment_WhenTfBuildTrueCasing_ReturnsTrue()
        => AzureDevOpsUtilities.IsAzureDevOpsEnvironment(Env(("TF_BUILD", "True"))).Should().BeTrue();

    [Fact]
    public void IsAzureDevOpsEnvironment_WhenTfBuildMissing_ReturnsFalse()
        => AzureDevOpsUtilities.IsAzureDevOpsEnvironment(Env()).Should().BeFalse();

    [Fact]
    public void IsAzureDevOpsEnvironment_WhenTfBuildNotBoolean_ReturnsFalse()
        => AzureDevOpsUtilities.IsAzureDevOpsEnvironment(Env(("TF_BUILD", "1"))).Should().BeFalse();

    [Theory]
    [InlineData("off")]
    [InlineData("Off")]
    [InlineData("false")]
    [InlineData("0")]
    public void IsAzureDevOpsEnvironment_WhenOptedOut_ReturnsFalse(string optOutValue)
        => AzureDevOpsUtilities.IsAzureDevOpsEnvironment(Env(("TF_BUILD", "true"), ("TESTINGPLATFORM_AZDO_OUTPUT", optOutValue)))
            .Should().BeFalse();

    [Fact]
    public void IsAzureDevOpsEnvironment_WhenOptOutIsOtherValue_ReturnsTrue()
        => AzureDevOpsUtilities.IsAzureDevOpsEnvironment(Env(("TF_BUILD", "true"), ("TESTINGPLATFORM_AZDO_OUTPUT", "on")))
            .Should().BeTrue();

    private static Func<string, string?> Env(params (string Key, string Value)[] variables)
    {
        var map = variables.ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);
        return key => map.TryGetValue(key, out string? value) ? value : null;
    }
}
