// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal static class TestCommandParser
{
    public static void ConfigureCommand(TestCommandDefinition command)
    {
        command.FrameworkOption.AddCompletions(CliCompletion.TargetFrameworksFromProjectFile);
        command.ConfigurationOption.AddCompletions(CliCompletion.ConfigurationsFromProjectFileOrDefaults);
        command.TargetPlatformOptions.RuntimeOption.AddCompletions(CliCompletion.RuntimesFromProjectFile);

        switch (command)
        {
            case TestCommandDefinition.VSTest vs:
                vs.SetAction(TestCommand.Run);
                break;

            case TestCommandDefinition.MicrosoftTestingPlatform mtp:
                var impl = new MicrosoftTestingPlatformTestCommand();
                mtp.SetAction(parseResult => impl.Run(parseResult, isHelp: false));
                mtp.CustomHelpLayoutProvider = impl;
                break;

            default:
                throw new NotSupportedException();
        }
    }
}
