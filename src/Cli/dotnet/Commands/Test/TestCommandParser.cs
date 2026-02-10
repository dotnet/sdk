// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.CommandLine;
using Command = System.CommandLine.Command;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal static class TestCommandParser
{
    private static readonly TestCommandDefinition Command = CreateCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static TestCommandDefinition CreateCommand()
    {
        var command = TestCommandDefinition.Create();

        command.TargetPlatformOptions.RuntimeOption.AddCompletions(CliCompletion.RunTimesFromProjectFile);

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
        };

        return command;
    }
}
