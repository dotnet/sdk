// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.ElevatedSystemPath;

internal static class ElevatedSystemPathCommandParser
{
    public static readonly Argument<string> OperationArgument = new("operation")
    {
        HelpName = "OPERATION",
        Description = Strings.ElevatedSystemPathOperationDescription,
        Arity = ArgumentArity.ExactlyOne,
    };

    public static readonly Argument<string> OutputFile = new("outputfile")
    {
        HelpName = "OUTPUT_FILE",
        Description = Strings.ElevatedSystemPathOutputFileDescription,
        Arity = ArgumentArity.ExactlyOne,
    };

    public static readonly Option<string> DotnetDir = new("--dotnet-dir")
    {
        HelpName = "DOTNET_DIR",
        Description = Strings.ElevatedSystemPathDotnetDirDescription,
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
        Command command = new("elevatedsystempath", Strings.ElevatedSystemPathCommandDescription);
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
            result.AddError(string.Format(CultureInfo.CurrentCulture, Strings.ElevatedSystemPathDotnetDirRequired, operation));
        }
    }
}
