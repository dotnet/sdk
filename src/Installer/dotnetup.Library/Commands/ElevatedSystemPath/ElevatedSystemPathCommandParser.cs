// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.ElevatedSystemPath;

internal static class ElevatedSystemPathCommandParser
{
    public static readonly Argument<string> OperationArgument = new("operation")
    {
        HelpName = "OPERATION",
        Description = "The operation to perform: 'insertdotnet' or 'removedotnet'",
        Arity = ArgumentArity.ExactlyOne,
    };

    public static readonly Argument<string> OutputFile = new("outputfile")
    {
        HelpName = "OUTPUT_FILE",
        Description = "A file where any output that should be displayed to the user should be written.",
        Arity = ArgumentArity.ExactlyOne,
    };

    public static readonly Option<string> DotnetDir = new("--dotnet-dir")
    {
        HelpName = "DOTNET_DIR",
        Description = "The dotnet directory to insert into or remove from the system PATH. "
            + "Required because the elevated process may run under a different account and cannot "
            + "recompute the invoking user's directory.",
        Required = true,
    };

    private static readonly Command s_elevatedSystemPathCommand = ConstructCommand();

    public static Command GetCommand()
    {
        return s_elevatedSystemPathCommand;
    }

    private static Command ConstructCommand()
    {
        Command command = new("elevatedsystempath", "Modifies the machine-wide system PATH (requires elevated privileges)");
        command.Hidden = true;

        command.Arguments.Add(OperationArgument);
        command.Arguments.Add(OutputFile);
        command.Options.Add(DotnetDir);

        command.SetAction(parseResult => new ElevatedSystemPathCommand(parseResult).Execute());

        return command;
    }
}
