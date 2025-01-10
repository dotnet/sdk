// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.CommandLine;

namespace FileRemover;

public class Program
{
    public static int ExitCode = 0;

    public static async Task<int> Main(string[] args)
    {
        var targetDirectoryArgument = new CliArgument<string>("target-directory")
        {
            Description = "The directory to run the removal on.",
            Arity = ArgumentArity.ExactlyOne
        };

        var removalFileOption = new CliOption<string>("--removal-file", "-rf")
        {
            Description = "The file containing the list of path to remove from the target directory.\n",
            Arity = ArgumentArity.ExactlyOne
        };

        var rootCommand = new CliRootCommand("Tool for removing specified files from the target directory.")
        {
            targetDirectoryArgument,
            removalFileOption
        };

        rootCommand.SetAction((parseResult, cancellationToken) =>
        {
            var targetDirectory = parseResult.GetValue(targetDirectoryArgument) ?? throw new ArgumentNullException("target-directory");
            var removalFile = parseResult.GetValue(removalFileOption) ?? throw new ArgumentNullException("removal-file");

            Remover.RemoveFiles(targetDirectory, removalFile);

            return Task.CompletedTask;
        });

        await rootCommand.Parse(args).InvokeAsync();

        return ExitCode;
    }
}
