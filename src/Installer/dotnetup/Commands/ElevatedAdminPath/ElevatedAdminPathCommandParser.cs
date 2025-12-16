// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.ElevatedAdminPath;

internal static class ElevatedAdminPathCommandParser
{
    public static readonly Argument<string> OperationArgument = new("operation")
    {
        HelpName = "OPERATION",
        Description = "The operation to perform: 'removedotnet' or 'adddotnet'",
        Arity = ArgumentArity.ExactlyOne,
    };

    private static readonly Command ElevatedAdminPathCommand = ConstructCommand();

    public static Command GetCommand()
    {
        return ElevatedAdminPathCommand;
    }

    private static Command ConstructCommand()
    {
        Command command = new("elevatedadminpath", "Modifies the machine-wide admin PATH (requires elevated privileges)");

        command.Arguments.Add(OperationArgument);

        command.SetAction(parseResult => new ElevatedAdminPathCommand(parseResult).Execute());

        return command;
    }
}
