// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.CommandLine;
using Microsoft.Extensions.Logging;

namespace BinaryToolKit;

public class Program
{
    public static readonly Argument<string> TargetDirectory = new("target-directory")
    {
        Description = "The directory to run the binary tooling on.",
        Arity = ArgumentArity.ExactlyOne
    };

    public static readonly Option<string> OutputReportDirectory = new("--output-directory", "-o")
    {
        Description = "The directory to output the report to.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = _ => Path.Combine(Directory.GetCurrentDirectory(), "binary-report")
    };

    public static readonly Option<LogLevel> Level = new("--log-level", "-l")
    {
        Description = "The log level to run the tool in.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = _ => LogLevel.Information,
        Recursive = true
    };

    public static readonly Option<string> AllowedBinariesFile = new("--allowed-binaries-file", "-ab")
    {
        Description = "The file containing the list of allowed binaries that are ignored for cleaning or validating.\n",
        Arity = ArgumentArity.ZeroOrOne
    };

    public static int ExitCode = 0;

    public static async Task<int> Main(string[] args)
    {
        var cleanCommand = CreateCommand("clean", "Clean the binaries in the target directory.");
        var validateCommand = CreateCommand("validate", "Detect new binaries in the target directory.");

        var rootCommand = new RootCommand("Tool for detecting, validating, and cleaning binaries in the target directory.")
        {
            Level,
            cleanCommand,
            validateCommand
        };

        SetCommandAction(cleanCommand, Modes.Clean);
        SetCommandAction(validateCommand, Modes.Validate);

        await rootCommand.Parse(args).InvokeAsync();

        return ExitCode;
    }

    private static Command CreateCommand(string name, string description)
    {
        return new Command(name, description)
        {
            TargetDirectory,
            OutputReportDirectory,
            AllowedBinariesFile
        };
    }

    private static void SetCommandAction(Command command, Modes mode)
    {
        command.SetAction(async (result, CancellationToken) =>
        {
            Log.Level = result.GetValue(Level);

            var binaryTool = new BinaryTool();

            ExitCode = await binaryTool.ExecuteAsync(
                result.GetValue(TargetDirectory)!,
                result.GetValue(OutputReportDirectory)!,
                result.GetValue(AllowedBinariesFile),
                mode);
        });
    }
}