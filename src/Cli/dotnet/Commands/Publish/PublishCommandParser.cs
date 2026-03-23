// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Publish;

internal static class PublishCommandParser
{
    public static void ConfigureCommand(PublishCommandDefinition command)
    {
        command.SetAction(PublishCommand.Run);
        command.FrameworkOption.AddCompletions(CliCompletion.TargetFrameworksFromProjectFile);
        command.ConfigurationOption.AddCompletions(CliCompletion.ConfigurationsFromProjectFileOrDefaults);
        command.TargetPlatformOptions.RuntimeOption.AddCompletions(CliCompletion.RuntimesFromProjectFile);
    }
}
