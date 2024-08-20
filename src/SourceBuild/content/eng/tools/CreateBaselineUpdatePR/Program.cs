// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.CommandLine;
using Microsoft.Extensions.Logging;

namespace CreateBaselineUpdatePR;

public class Program
{
    public static readonly CliArgument<string> Repo = new("repo")
    {
        Description = "The GitHub repository to create the PR in. Should be in the form '<owner>/<repo-name>'",
        Arity = ArgumentArity.ExactlyOne
    };

    public static readonly CliArgument<string> OriginalFilesDirectory = new("original-files-directory")
    {
        Description = "The directory where the original test files are located. Should be relative to the repo",
        Arity = ArgumentArity.ExactlyOne
    };

    public static readonly CliArgument<string> UpdatedFilesDirectory = new("updated-files-directory")
    {
        Description = "The directory containing the updated test files published by the associated test. Should be absolute or relative to the working directory of the tool.",
        Arity = ArgumentArity.ExactlyOne
    };

    public static readonly CliArgument<int> BuildId = new("build-id")
    {
        Description = "The id of the build that published the updated test files.",
        Arity = ArgumentArity.ExactlyOne
    };

    public static readonly CliOption<string> Title = new("--title", "-t")
    {
        Description = "The title of the PR.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = _ => "Update Test Baselines and Exclusions"
    };

    public static readonly CliOption<string> Branch = new("--branch", "-b")
    {
        Description = "The target branch of the PR.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = _ => "main"
    };

    public static readonly CliOption<string> GitHubToken = new("--github-token", "-g")
    {
        Description = "The GitHub token to use to create the PR.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = _ => Environment.GetEnvironmentVariable("GH_TOKEN") ?? throw new ArgumentException("GitHub token not provided.")
    };

    public static readonly CliOption<LogLevel> Level = new("--log-level", "-l")
    {
        Description = "The log level to run the tool in.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = _ => LogLevel.Information,
        Recursive = true
    };

    public static int ExitCode = 0;

    public static async Task<int> Main(string[] args)
    {
        var sdkDiffTestsCommand = CreateCommand("sdk", "Creates a PR that updates baselines and exclusion files published by the sdk diff tests.");
        var licenseScanTestsCommand = CreateCommand("license", "Creates a PR that updates baselines and exclusion files published by the license scan tests.");

        var rootCommand = new CliRootCommand("Tool for creating PRs that update baselines and exclusion files.")
        {
            Level,
            sdkDiffTestsCommand,
            licenseScanTestsCommand
        };

        SetCommandAction(sdkDiffTestsCommand, Pipelines.Sdk);
        SetCommandAction(licenseScanTestsCommand, Pipelines.License);

        await rootCommand.Parse(args).InvokeAsync();

        return ExitCode;
    }

    private static CliCommand CreateCommand(string name, string description)
    {
        return new CliCommand(name, description)
        {
            Repo,
            OriginalFilesDirectory,
            UpdatedFilesDirectory,
            BuildId,
            Title,
            Branch,
            GitHubToken
        };
    }

    private static void SetCommandAction(CliCommand command, Pipelines pipeline)
    {
        command.SetAction(async (result, CancellationToken) =>
        {
            Log.Level = result.GetValue(Level);

            var creator = new PRCreator(result.GetValue(Repo)!, result.GetValue(GitHubToken)!);

            ExitCode = await creator.ExecuteAsync(
                result.GetValue(OriginalFilesDirectory)!,
                result.GetValue(UpdatedFilesDirectory)!,
                result.GetValue(BuildId)!,
                result.GetValue(Title)!,
                result.GetValue(Branch)!,
                pipeline);
        });
    }
}