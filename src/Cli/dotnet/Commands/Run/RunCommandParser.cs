// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Run;

internal static class RunCommandParser
{
    [RequiresDynamicCode("Uses MSBuild Object Model types, which are not AOT-safe")]
    public static void ConfigureCommand(RunCommandDefinition command)
    {
        command.SetAction(RunCommand.Run);
        command.FrameworkOption.AddCompletions(CliCompletion.TargetFrameworksFromProjectFile);
        command.ConfigurationOption.AddCompletions(CliCompletion.ConfigurationsFromProjectFileOrDefaults);
        command.TargetPlatformOptions.RuntimeOption.AddCompletions(CliCompletion.RuntimesFromProjectFile);
    }
}
