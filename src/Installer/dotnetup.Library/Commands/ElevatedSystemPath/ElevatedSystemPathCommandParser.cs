// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.ElevatedSystemPath;

internal static class ElevatedSystemPathCommandParser
{
    public static readonly Argument<string> OperationArgument = new("operation")
    {
        HelpName = "OPERATION",
        Description = "The operation to perform: 'insertdotnet', 'removedotnet', 'enableadminmode', or 'disableadminmode'",
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
            + "Required for 'insertdotnet' and 'removedotnet' because the elevated process may run under a "
            + "different account and cannot recompute the invoking user's directory.",
    };

    internal const string InsertDotnetOperation = "insertdotnet";
    internal const string RemoveDotnetOperation = "removedotnet";
    internal const string EnableAdminModeOperation = "enableadminmode";
    internal const string DisableAdminModeOperation = "disableadminmode";

    internal static bool RequiresDotnetDir(string? operation) =>
        string.Equals(operation, InsertDotnetOperation, StringComparison.OrdinalIgnoreCase)
        || string.Equals(operation, RemoveDotnetOperation, StringComparison.OrdinalIgnoreCase);

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
        command.Validators.Add(ValidateDotnetDir);

        command.SetAction(parseResult => new ElevatedSystemPathCommand(parseResult).Execute());

        return command;
    }

    private static void ValidateDotnetDir(CommandResult result)
    {
        string? operation = result.GetValue(OperationArgument);
        if (RequiresDotnetDir(operation) && string.IsNullOrWhiteSpace(result.GetValue(DotnetDir)))
        {
            result.AddError($"The '{operation}' operation requires the --dotnet-dir option.");
        }
    }
}
