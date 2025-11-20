// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Command = System.CommandLine.Command;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal static class TestCommandParser
{
    private static readonly Command Command = CreateCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command CreateCommand()
    {
        return TestCommandDefinition.GetTestRunner() switch
        {
            TestCommandDefinition.TestRunner.VSTest => CreateVSTestCommand(),
            TestCommandDefinition.TestRunner.MicrosoftTestingPlatform => CreateTestingPlatformCommand(),
            _ => throw new NotSupportedException(),
        };
    }

    private static Command CreateTestingPlatformCommand()
    {
        var command = new MicrosoftTestingPlatformTestCommand(TestCommandDefinition.Name);
        TestCommandDefinition.ConfigureTestingPlatformCommand(command);
        command.SetAction(parseResult => command.Run(parseResult));
        return command;
    }

    private static Command CreateVSTestCommand()
    {
        var command = new Command(TestCommandDefinition.Name);
        TestCommandDefinition.ConfigureVSTestCommand(command);
        command.SetAction(TestCommand.Run);
        return command;
    }
}
