// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests;

public class VerbosityActionTests
{
    [Theory]
    [InlineData("diag")]
    [InlineData("diagnostic")]
    public void DiagnosticVerbosity_SetsEnvironmentVariable_Build(string verbosityValue)
    {
        // Arrange - clear any existing value
        Environment.SetEnvironmentVariable(CommandLoggingContext.Variables.Verbose, null);
        CommandLoggingContext.SetVerbose(false);
        
        // Act
        var result = Parser.Parse(["build", "--verbosity", verbosityValue, "--help"]);
        result.Invoke();
        
        // Assert
        Environment.GetEnvironmentVariable(CommandLoggingContext.Variables.Verbose)
            .Should()
            .Be(bool.TrueString);
        
        CommandLoggingContext.IsVerbose.Should().BeTrue();
        
        // Cleanup
        Environment.SetEnvironmentVariable(CommandLoggingContext.Variables.Verbose, null);
        CommandLoggingContext.SetVerbose(false);
    }

    [Theory]
    [InlineData("quiet")]
    [InlineData("minimal")]
    [InlineData("normal")]
    [InlineData("detailed")]
    public void NonDiagnosticVerbosity_DoesNotSetEnvironmentVariable_Build(string verbosityValue)
    {
        // Arrange - clear any existing value
        Environment.SetEnvironmentVariable(CommandLoggingContext.Variables.Verbose, null);
        CommandLoggingContext.SetVerbose(false);
        
        // Act
        var result = Parser.Parse(["build", "--verbosity", verbosityValue, "--help"]);
        result.Invoke();
        
        // Assert
        Environment.GetEnvironmentVariable(CommandLoggingContext.Variables.Verbose)
            .Should().BeNullOrEmpty();
        
        CommandLoggingContext.IsVerbose.Should().BeFalse();
    }

    [Theory]
    [InlineData("restore")]
    [InlineData("publish")]
    [InlineData("pack")]
    public void DiagnosticVerbosity_SetsEnvironmentVariable_MultipleCommands(string commandName)
    {
        // Arrange - clear any existing value
        Environment.SetEnvironmentVariable(CommandLoggingContext.Variables.Verbose, null);
        CommandLoggingContext.SetVerbose(false);
        
        // Act
        var result = Parser.Parse([commandName, "--verbosity", "diag", "--help"]);
        result.Invoke();
        
        // Assert
        Environment.GetEnvironmentVariable(CommandLoggingContext.Variables.Verbose)
            .Should()
            .Be(bool.TrueString);
        
        CommandLoggingContext.IsVerbose.Should().BeTrue();
        
        // Cleanup
        Environment.SetEnvironmentVariable(CommandLoggingContext.Variables.Verbose, null);
        CommandLoggingContext.SetVerbose(false);
    }
}
