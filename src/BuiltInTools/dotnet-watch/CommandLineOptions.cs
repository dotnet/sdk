// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Collections.Immutable;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Data;
using System.Diagnostics;
using Microsoft.DotNet.Watcher.Tools;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher;

internal sealed class CommandLineOptions
{
    public const string DefaultCommand = "run";

    private static readonly ImmutableArray<string> s_knownCommands =
    [
        "add",
        "build",
        "build-server",
        "clean",
        "format",
        "help",
        "list",
        "msbuild",
        "new",
        "nuget",
        "pack",
        "publish",
        "remove",
        "restore",
        "run",
        "sdk",
        "sln",
        "store",
        "test",
        "tool",
        "vstest",
        "workload"
    ];

    public bool List { get; init; }
    required public GlobalOptions GlobalOptions { get; init; }

    public string? ProjectPath { get; init; }
    public string? TargetFramework { get; init; }
    public IReadOnlyList<(string name, string value)>? BuildProperties { get; init; }
    public bool NoLaunchProfile { get; init; }
    public string? LaunchProfileName { get; init; }

    public string? ExplicitCommand { get; init; }

    public required IReadOnlyList<string> CommandArguments { get; init; }

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
        var targetFrameworkOption = new CliOption<string>("--framework", "-f") { Hidden = true, Arity = ArgumentArity.ZeroOrOne, AllowMultipleArgumentsPerToken = false };
        var propertyOption = new CliOption<string[]>("--property") { Hidden = true, Arity = ArgumentArity.OneOrMore, AllowMultipleArgumentsPerToken = false };

        var rootCommand = new CliRootCommand(Resources.Help)
        {
            Directives = { new EnvironmentVariablesDirective() }
        };

        foreach (var watchOption in watchOptions)
        {
            rootCommand.Options.Add(watchOption);
        }

        rootCommand.Options.Add(longProjectOption);
        rootCommand.Options.Add(shortProjectOption);
        rootCommand.Options.Add(launchProfileOption);
        rootCommand.Options.Add(noLaunchProfileOption);
        rootCommand.Options.Add(targetFrameworkOption);
        rootCommand.Options.Add(propertyOption);

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
        };

        var parseResult = rootCommand.Parse(args, cliConfig);
        if (parseResult.Errors.Any())
        {
            foreach (var error in parseResult.Errors)
            {
                reporter.Error(error.Message);
            }

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

            CommandArguments = GetCommandArguments(parseResult, watchOptions, out var explicitCommand),
            ExplicitCommand = explicitCommand,

            ProjectPath = projectValue,
            LaunchProfileName = parseResult.GetValue(launchProfileOption),
            NoLaunchProfile = parseResult.GetValue(noLaunchProfileOption),
            TargetFramework = parseResult.GetValue(targetFrameworkOption),
            BuildProperties = ParseBuildProperties(parseResult.GetValue(propertyOption) ?? []).ToArray(),
        };

        // Parses name=value pairs passed to --property. Skips invalid input. 
        // We don't report error here as it will be reported by dotnet run.
        static IEnumerable<(string key, string value)> ParseBuildProperties(string[] properties)
            => from property in properties
               let index = property.IndexOf('=')
               where index >= 0
               let name = property[..index].Trim()
               let value = property[(index + 1)..]
               where name is not []
               select (name, value);
    }

    private static IReadOnlyList<string> GetCommandArguments(
        ParseResult parseResult,
        IReadOnlyList<CliOption> watchOptions,
        out string? explicitCommand)
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

        explicitCommand = null;
        var dashDashInserted = false;

        for (int i = 0; i < parseResult.UnmatchedTokens.Count; i++)
        {
            var token = parseResult.UnmatchedTokens[i];

            // command token can't follow "--"
            if (i < unmatchedTokensBeforeDashDash && explicitCommand == null && s_knownCommands.Contains(token))
            {
                explicitCommand = token;
            }
            else 
            {
                if (!dashDashInserted && i >= unmatchedTokensBeforeDashDash)
                {
                    arguments.Add("--");
                    dashDashInserted = true;
                }

                arguments.Add(token);
            }
        }

        return arguments;
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
            BuildProperties = BuildProperties ?? [],
            Command = Command,
            CommandArguments = CommandArguments,
            LaunchEnvironmentVariables = [],
            LaunchProfileName = LaunchProfileName,
            NoLaunchProfile = NoLaunchProfile,
            TargetFramework = TargetFramework,
        };
}
