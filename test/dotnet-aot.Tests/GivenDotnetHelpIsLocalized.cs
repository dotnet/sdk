// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

namespace Microsoft.DotNet.Cli.Tests;

[TestClass]
[TestCategory("AOT")]
public class GivenDotnetHelpIsLocalized : SdkTest
{
    [TestMethod]
    [DataRow("de-DE", ".NET-Generator", false)]
    [DataRow("de-DE", ".NET-Generator", true)]
    [DataRow("fr-FR", "G\u00e9n\u00e9rateur .NET", false)]
    [DataRow("fr-FR", "G\u00e9n\u00e9rateur .NET", true)]
    [DataRow("zh-CN", ".NET \u751f\u6210\u5668", false)]
    [DataRow("zh-CN", ".NET \u751f\u6210\u5668", true)]
    public void Build_help_uses_the_requested_language(string language, string description, bool enableAot)
    {
        ReadHelp(language, enableAot, "build").Should().Contain(description).And.NotContain(".NET Builder");
    }

    [TestMethod]
    [CombinatorialData]
    public void Native_help_matches_managed_help(
        [CombinatorialValues("cs-CZ", "de-DE", "es-ES", "fr-FR", "it-IT", "ja-JP", "ko-KR", "pl-PL", "pt-BR", "ru-RU", "tr-TR", "zh-CN", "zh-TW")] string language,
        [CombinatorialValues("build", "restore", "sln")] string command)
    {
        ReadHelp(language, enableAot: true, command).Should().Be(ReadHelp(language, enableAot: false, command));
    }

    private string ReadHelp(string language, bool enableAot, string command)
    {
        var result = new DotnetCommand(Log)
            .WithWorkingDirectory(SdkTestContext.Current.TestExecutionDirectory)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", language)
            .WithEnvironmentVariable("DOTNET_CLI_ENABLEAOT", enableAot.ToString())
            .WithEnvironmentVariable("DOTNET_NOLOGO", "1")
            .Execute(command, "--help");

        result.Should().Pass();
        result.StdErr.Should().BeNullOrEmpty();
        Assert.IsNotNull(result.StdOut);
        return result.StdOut;
    }
}
