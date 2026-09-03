// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Tests.ParserTests;

[TestClass]
public class CommonOptionsTests
{
    [TestMethod]
    public void ConfigurationDefaultsToEnvironmentVariable()
    {
        string? originalConfiguration = Environment.GetEnvironmentVariable("Configuration");

        try
        {
            Environment.SetEnvironmentVariable("Configuration", "EnvironmentConfiguration");
            var command = new RootCommand();
            var option = CommonOptions.CreateConfigurationOption("Configuration");
            command.Options.Add(option);

            var result = command.Parse([]);

            result.GetValue(option).Should().Be("EnvironmentConfiguration");
            result.OptionValuesToBeForwarded(command).Should().ContainSingle()
                .Which.Should().Be("--property:Configuration=EnvironmentConfiguration");
        }
        finally
        {
            Environment.SetEnvironmentVariable("Configuration", originalConfiguration);
        }
    }

    [TestMethod]
    public void ExplicitConfigurationOverridesEnvironmentVariable()
    {
        string? originalConfiguration = Environment.GetEnvironmentVariable("Configuration");

        try
        {
            Environment.SetEnvironmentVariable("Configuration", "EnvironmentConfiguration");
            var command = new RootCommand();
            var option = CommonOptions.CreateConfigurationOption("Configuration");
            command.Options.Add(option);

            var result = command.Parse(["--configuration", "ExplicitConfiguration"]);

            result.GetValue(option).Should().Be("ExplicitConfiguration");
            result.OptionValuesToBeForwarded(command).Should().ContainSingle()
                .Which.Should().Be("--property:Configuration=ExplicitConfiguration");
        }
        finally
        {
            Environment.SetEnvironmentVariable("Configuration", originalConfiguration);
        }
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("\t")]
    public void EmptyOrWhitespaceConfigurationEnvironmentVariableIsIgnored(string configuration)
    {
        string? originalConfiguration = Environment.GetEnvironmentVariable("Configuration");

        try
        {
            Environment.SetEnvironmentVariable("Configuration", configuration);
            var command = new RootCommand();
            var option = CommonOptions.CreateConfigurationOption("Configuration");
            command.Options.Add(option);

            var result = command.Parse([]);

            result.GetValue(option).Should().BeNull();
            result.OptionValuesToBeForwarded(command).Should().BeEmpty();
        }
        finally
        {
            Environment.SetEnvironmentVariable("Configuration", originalConfiguration);
        }
    }

    [TestMethod]
    public void Duplicates()
    {
        var command = new RootCommand();
        var option = CommonOptions.CreateEnvOption();

        command.Options.Add(option);

        var result = command.Parse(["-e", "A=1", "-e", "A=2"]);

        result.GetValue(option)
            .Should()
            .BeEquivalentTo(new Dictionary<string, string> { ["A"] = "2" });

        result.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public void Duplicates_CasingDifference()
    {
        var command = new RootCommand();
        var option = CommonOptions.CreateEnvOption();
        command.Options.Add(option);

        var result = command.Parse(["-e", "A=1", "-e", "a=2"]);

        var expected = new Dictionary<string, string>();
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            expected.Add("A", "2");
        }
        else
        {
            expected.Add("A", "1");
            expected.Add("a", "2");
        }

        result.GetValue(option)
            .Should()
            .BeEquivalentTo(expected);

        result.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public void MultiplePerToken()
    {
        var command = new RootCommand();
        var option = CommonOptions.CreateEnvOption();
        command.Options.Add(option);

        var result = command.Parse(["-e", "A=1;B=2,C=3 D=4", "-e", "B==Y=", "-e", "C;=;"]);

        result.GetValue(option)
            .Should()
            .BeEquivalentTo(new Dictionary<string, string>
            {
                ["A"] = "1;B=2,C=3 D=4",
                ["B"] = "=Y=",
                ["C;"] = ";"
            });

        result.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public void NoValue()
    {
        var command = new RootCommand();
        var option = CommonOptions.CreateEnvOption();
        command.Options.Add(option);

        var result = command.Parse(["-e", "A"]);

        result.GetValue(option)
            .Should()
            .BeEquivalentTo(new Dictionary<string, string> { ["A"] = "" });

        result.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public void WhitespaceTrimming()
    {
        var command = new RootCommand();
        var option = CommonOptions.CreateEnvOption();
        command.Options.Add(option);

        var result = command.Parse(["-e", " A \t\n\r\u2002 = X Y \t\n\r\u2002"]);

        result.GetValue(option)
            .Should()
            .BeEquivalentTo(new Dictionary<string, string> { ["A"] = " X Y \t\n\r\u2002" });

        result.Errors.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("=")]
    [DataRow("= X")]
    [DataRow("  \u2002 = X")]
    public void Errors(string token)
    {
        var command = new RootCommand();
        var option = CommonOptions.CreateEnvOption();
        command.Options.Add(option);

        var result = command.Parse(["-e", token]);

        result.Errors.Select(e => e.Message).Should().BeEquivalentTo(
        [
            string.Format(CliStrings.IncorrectlyFormattedEnvironmentVariables, $"'{token}'")
        ]);
    }
}
