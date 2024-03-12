// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.CommandLine;
using Microsoft.Extensions.Logging;

namespace BinaryToolKit;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        CliArgument<string> TargetDirectory = new("target-directory")
        {
            Description = "The directory to run the binary tooling on."
        };

        CliArgument<string> OutputReportDirectory = new("output-report-directory")
        {
            Description = "The directory to output the report to."
        };

        CliOption<string> AllowedBinariesFile = new("--allowed-binaries", "-ab")
        {
            Description = "The file containing the list of known binaries " +
                    "that are allowed in the VMR and can be kept for source-building."
        };

        CliOption<string> DisallowedSbBinariesFile = new("--disallowed-sb-binaries", "-db")
        {
            Description = "The file containing the list of known binaries " +
                        "that are allowed in the VMR but cannot be kept for source-building."
        };

        CliOption<Modes> Mode = new("--mode", "-m")
        {
            Description = "The mode to run the tool in.",
            Arity = ArgumentArity.ZeroOrOne,
            DefaultValueFactory = _ => Modes.All
        };

        CliOption<LogLevel> Level = new("--log-level", "-l")
        {
            Description = "The log level to run the tool in.",
            Arity = ArgumentArity.ZeroOrOne,
            DefaultValueFactory = _ => LogLevel.Information
        };

        var rootCommand = new CliRootCommand("Tool for detecting, validating, and cleaning binaries in the target directory.")
        {
            TargetDirectory,
            OutputReportDirectory,
            AllowedBinariesFile,
            DisallowedSbBinariesFile,
            Mode,
            Level
        };

        rootCommand.SetAction(async (result, CancellationToken) =>
        {
            Log.Level = result.GetValue(Level);
            
            var binaryTool = new BinaryTool();

            await binaryTool.ExecuteAsync(
                result.GetValue(TargetDirectory)!,
                result.GetValue(OutputReportDirectory)!,
                result.GetValue(AllowedBinariesFile),
                result.GetValue(DisallowedSbBinariesFile),
                result.GetValue(Mode));
        });

        return await rootCommand.Parse(args).InvokeAsync();
    }
}