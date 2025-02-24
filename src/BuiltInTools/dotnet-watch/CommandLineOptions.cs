// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Collections.Immutable;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Data;
using System.Diagnostics;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Tools.Run;
using NuGet.Common;

namespace Microsoft.DotNet.Watch;

internal sealed class CommandLineOptions
{
    public const string DefaultCommand = "run";

    public bool List { get; init; }
    required public GlobalOptions GlobalOptions { get; init; }

    public string? ProjectPath { get; init; }
    public string? TargetFramework { get; init; }
    public bool NoLaunchProfile { get; init; }
    public string? LaunchProfileName { get; init; }

    public string? ExplicitCommand { get; init; }

    public required IReadOnlyList<string> CommandArguments { get; init; }
    public required IReadOnlyList<string> BuildArguments { get; init; }

    public string Command => ExplicitCommand ?? DefaultCommand;

    public static CommandLineOptions? Parse(IReadOnlyList<string> args, IReporter reporter, TextWriter output, out int errorCode)
    {
        // dotnet watch specific options:

        var quietOption = new CliOption<bool>("--quiet", "-q") { Description = Resources.Help_Quiet };
        var verboseOption = new CliOption<bool>("--verbose") { Description = Resources.Help_Verbose };
        var listOption = new CliOption<bool>("--list") { Description = Resources.Help_List };
        var noHotReloadOption = new CliOption<bool>("--no-hot-reload") { Description = Resources.Help_NoHotReload };
        var nonInteractiveOption = new CliOption<bool>("--non-interactive") { Description = Resources.Help_NonInteractive };

        verboseOption.Validators.Add(v =>
        {
            if (v.GetResult(quietOption) is not null && v.GetResult(verboseOption) is not null)
            {
                v.AddError(Resources.Error_QuietAndVerboseSpecified);
            }
        });

        CliOption[] watchOptions =
        [
            quietOption,
            verboseOption,
            noHotReloadOption,
            nonInteractiveOption,
            listOption,
        ];

        // Options we need to know about that are passed through to the subcommand:

        var shortProjectOption = new CliOption<string>("-p") { Hidden = true, Arity = ArgumentArity.ZeroOrOne, AllowMultipleArgumentsPerToken = false };
        var longProjectOption = new CliOption<string>("--project") { Hidden = true, Arity = ArgumentArity.ZeroOrOne, AllowMultipleArgumentsPerToken = false };
        var launchProfileOption = new CliOption<string>("--launch-profile", "-lp") { Hidden = true, Arity = ArgumentArity.ZeroOrOne, AllowMultipleArgumentsPerToken = false };
        var noLaunchProfileOption = new CliOption<bool>("--no-launch-profile") { Hidden = true };

        var rootCommand = new CliRootCommand(Resources.Help)
        {
            Directives = { new EnvironmentVariablesDirective() },
        };

        foreach (var watchOption in watchOptions)
        {
            rootCommand.Options.Add(watchOption);
        }

        rootCommand.Options.Add(longProjectOption);
        rootCommand.Options.Add(shortProjectOption);
        rootCommand.Options.Add(launchProfileOption);
        rootCommand.Options.Add(noLaunchProfileOption);

        // We process all tokens that do not match any of the above options
        // to find the subcommand (the first unmatched token preceding "--")
        // and all its options and arguments.
        rootCommand.TreatUnmatchedTokensAsErrors = false;

        // We parse the command line outside of the action since the action
        // might not be invoked in presence of unmatched tokens.
        // We just need to know if the root command was invoked to handle --help.
        var rootCommandInvoked = false;
        rootCommand.SetAction(parseResult => rootCommandInvoked = true);

        var cliConfig = new CliConfiguration(rootCommand)
        {
            Output = output,
            Error = output,

            // To match dotnet command line parsing (see https://github.com/dotnet/sdk/blob/4712b35b94f2ad672e69ec35097cf86fc16c2e5e/src/Cli/dotnet/Parser.cs#L169):
            EnablePosixBundling = false,
        };

        // parse without forwarded options first:
        var parseResult = rootCommand.Parse(args, cliConfig);
        if (ReportErrors(parseResult, reporter))
        {
            errorCode = 1;
            return null;
        }

        // determine subcommand:
        var explicitCommand = TryGetSubcommand(parseResult);
        var command = explicitCommand ?? RunCommandParser.GetCommand();
        var buildOptions = command.Options.Where(o => o is IForwardedOption);

        foreach (var buildOption in buildOptions)
        {
            rootCommand.Options.Add(buildOption);
        }

        // reparse with forwarded options:
        parseResult = rootCommand.Parse(args, cliConfig);
        if (ReportErrors(parseResult, reporter))
        {
            errorCode = 1;
            return null;
        }

        // invoke to execute default actions for displaying help
        errorCode = parseResult.Invoke();
        if (!rootCommandInvoked)
        {
            // help displayed:
            return null;
        }

        var projectValue = parseResult.GetValue(longProjectOption);
        if (string.IsNullOrEmpty(projectValue))
        {
            var projectShortValue = parseResult.GetValue(shortProjectOption);
            if (!string.IsNullOrEmpty(projectShortValue))
            {
                reporter.Warn(Resources.Warning_ProjectAbbreviationDeprecated);
                projectValue = projectShortValue;
            }
        }

        var commandArguments = GetCommandArguments(parseResult, watchOptions, explicitCommand);

        // We assume that forwarded options, if any, are intended for dotnet build.
        var buildArguments = buildOptions.Select(option => ((IForwardedOption)option).GetForwardingFunction()(parseResult)).SelectMany(args => args).ToArray();
        var targetFrameworkOption = (CliOption<string>?)buildOptions.SingleOrDefault(option => option.Name == "--framework");

        return new()
        {
            List = parseResult.GetValue(listOption),
            GlobalOptions = new()
            {
                Quiet = parseResult.GetValue(quietOption),
                NoHotReload = parseResult.GetValue(noHotReloadOption),
                NonInteractive = parseResult.GetValue(nonInteractiveOption),
                Verbose = parseResult.GetValue(verboseOption),
            },

            CommandArguments = commandArguments,
            ExplicitCommand = explicitCommand?.Name,

            ProjectPath = projectValue,
            LaunchProfileName = parseResult.GetValue(launchProfileOption),
            NoLaunchProfile = parseResult.GetValue(noLaunchProfileOption),
            BuildArguments = buildArguments,
            TargetFramework = targetFrameworkOption != null ? parseResult.GetValue(targetFrameworkOption) : null,
        };
    }

    private static IReadOnlyList<string> GetCommandArguments(
        ParseResult parseResult,
        IReadOnlyList<CliOption> watchOptions,
        CliCommand? explicitCommand)
    {
        var arguments = new List<string>();

        foreach (var child in parseResult.CommandResult.Children)
        {
            var optionResult = (OptionResult)child;

            // skip watch options:
            if (!watchOptions.Contains(optionResult.Option))
            {
                Debug.Assert(optionResult.IdentifierToken != null);

                if (optionResult.Tokens.Count == 0)
                {
                    arguments.Add(optionResult.IdentifierToken.Value);
                }
                else if (optionResult.Option.Name == "--property")
                {
                    foreach (var token in optionResult.Tokens)
                    {
                        // While dotnet-build allows "/p Name=Value", dotnet-msbuild does not.
                        // Any command that forwards args to dotnet-msbuild will fail if we don't use colon.
                        // See https://github.com/dotnet/sdk/issues/44655.
                        arguments.Add($"{optionResult.IdentifierToken.Value}:{token.Value}");
                    }
                }
                else
                {
                    foreach (var token in optionResult.Tokens)
                    {
                        arguments.Add(optionResult.IdentifierToken.Value);
                        arguments.Add(token.Value);
                    }
                }
            }
        }

        var tokens = parseResult.UnmatchedTokens.ToArray();

        // Assuming that all tokens after "--" are unmatched:
        var dashDashIndex = IndexOf(parseResult.Tokens, t => t.Value == "--");
        var unmatchedTokensBeforeDashDash = parseResult.UnmatchedTokens.Count - (dashDashIndex >= 0 ? parseResult.Tokens.Count - dashDashIndex - 1 : 0);

        var seenCommand = false;
        var dashDashInserted = false;

        for (int i = 0; i < parseResult.UnmatchedTokens.Count; i++)
        {
            var token = parseResult.UnmatchedTokens[i];

            if (i < unmatchedTokensBeforeDashDash && !seenCommand && token == explicitCommand?.Name)
            {
                seenCommand = true;
                continue;
            }

            if (!dashDashInserted && i >= unmatchedTokensBeforeDashDash)
            {
                arguments.Add("--");
                dashDashInserted = true;
            }

            arguments.Add(token);
        }

        return arguments;
    }

    private static CliCommand? TryGetSubcommand(ParseResult parseResult)
    {
        // Assuming that all tokens after "--" are unmatched:
        var dashDashIndex = IndexOf(parseResult.Tokens, t => t.Value == "--");
        var unmatchedTokensBeforeDashDash = parseResult.UnmatchedTokens.Count - (dashDashIndex >= 0 ? parseResult.Tokens.Count - dashDashIndex - 1 : 0);

        var knownCommandsByName = Parser.Subcommands.ToDictionary(keySelector: c => c.Name, elementSelector: c => c);

        for (int i = 0; i < unmatchedTokensBeforeDashDash; i++)
        {
            // command token can't follow "--"
            if (knownCommandsByName.TryGetValue(parseResult.UnmatchedTokens[i], out var explicitCommand))
            {
                return explicitCommand;
            }
        }

        return null;
    }

    private static bool ReportErrors(ParseResult parseResult, IReporter reporter)
    {
        if (parseResult.Errors.Any())
        {
            foreach (var error in parseResult.Errors)
            {
                reporter.Error(error.Message);
            }

            return true;
        }

        return false;
    }

    private static int IndexOf<T>(IReadOnlyList<T> list, Func<T, bool> predicate)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (predicate(list[i]))
            {
                return i;
            }
        }

        return -1;
    }

    public ProjectOptions GetProjectOptions(string projectPath, string workingDirectory)
        => new()
        {
            IsRootProject = true,
            ProjectPath = projectPath,
            WorkingDirectory = workingDirectory,
            Command = Command,
            CommandArguments = CommandArguments,
            LaunchEnvironmentVariables = [],
            LaunchProfileName = LaunchProfileName,
            NoLaunchProfile = NoLaunchProfile,
            BuildArguments = BuildArguments,
            TargetFramework = TargetFramework,
        };

    // Parses name=value pairs passed to --property. Skips invalid input.
    public static IEnumerable<(string key, string value)> ParseBuildProperties(IEnumerable<string> arguments)
        => from argument in arguments
           let colon = argument.IndexOf(':')
           where colon >= 0 && argument[0..colon] is "--property" or "-property" or "/property" or "/p" or "-p" or "--p"
           let eq = argument.IndexOf('=', colon)
           where eq >= 0
           let name = argument[(colon + 1)..eq].Trim()
           let value = argument[(eq + 1)..]
           where name is not []
           select (name, value);

    /// <summary>
    /// Returns true if the command executes the code of the target project.
    /// </summary>
    public static bool IsCodeExecutionCommand(string commandName)
        => commandName is "run" or "test";
}
