// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Pack;

internal static class PackCommandParser
{
    [RequiresDynamicCode("Uses MSBuild Object Model types, which are not AOT-safe")]
    public static void ConfigureCommand(PackCommandDefinition command)
    {
        command.ConfigurationOption.AddCompletions(CliCompletion.ConfigurationsFromProjectFileOrDefaults);
        command.SetAction(PackCommand.Run);
    }
}
