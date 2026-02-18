// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Clean.FileBasedAppArtifacts;

namespace Microsoft.DotNet.Cli.Commands.Clean;

internal static class CleanCommandParser
{
    public static void ConfigureCommand(CleanCommandDefinition command)
    {
        command.SetAction(CleanCommand.Run);
        command.FrameworkOption.AddCompletions(CliCompletion.TargetFrameworksFromProjectFile);
        command.ConfigurationOption.AddCompletions(CliCompletion.ConfigurationsFromProjectFileOrDefaults);
        command.FileBasedAppsCommand.SetAction(parseResult => new CleanFileBasedAppArtifactsCommand(parseResult).Execute());
    }
}
