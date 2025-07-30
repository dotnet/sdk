// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Data;
using System.Diagnostics;
using Microsoft.Build.Logging;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Watch;

internal sealed class CommandLineOptions
{
    public const string DefaultCommand = "run";

    private static readonly ImmutableArray<string> s_binaryLogOptionNames = ["-bl", "/bl", "-binaryLogger", "--binaryLogger", "/binaryLogger"];

    public bool List { get; init; }
    public required GlobalOptions GlobalOptions { get; init; }

    public string? ProjectPath { get; init; }
    public string? TargetFramework { get; init; }
    public bool NoLaunchProfile { get; init; }
    public string? LaunchProfileName { get; init; }

    /// <summary>
    /// Arguments passed to <see cref="Command"/>.
    /// </summary>
    public required IReadOnlyList<string> CommandArguments { get; init; }

    /// <summary>
    /// Arguments passed to `dotnet build` and to design-time build evaluation.
    /// </summary>
    public required IReadOnlyList<string> BuildArguments { get; init; }

    public string? ExplicitCommand { get; init; }

    public string Command => ExplicitCommand ?? DefaultCommand;

    // this option is referenced from inner logic and so needs to be reference-able
    public static Option<bool> NonInteractiveOption = new Option<bool>("--non-interactive") { Description = Resources.Help_NonInteractive, Arity = ArgumentArity.Zero };

    public static CommandLineOptions? Parse(IReadOnlyList<string> args, IReporter reporter, TextWriter output, out int errorCode)
    {
        // dotnet watch specific options:
        var quietOption = new Option<bool>("--quiet", "-q") { Description = Resources.Help_Quiet, Arity = ArgumentArity.Zero };
        var verboseOption = new Option<bool>("--verbose") { Description = Resources.Help_Verbose, Arity = ArgumentArity.Zero };
        var listOption = new Option<bool>("--list") { Description = Resources.Help_List, Arity = ArgumentArity.Zero };
        var noHotReloadOption = new Option<bool>("--no-hot-reload") { Description = Resources.Help_NoHotReload, Arity = ArgumentArity.Zero };

        verboseOption.Validators.Add(v =>
        {
            if (v.GetValue(quietOption) && v.GetValue(verboseOption))
            {
                v.AddError(Resources.Error_QuietAndVerboseSpecified);
            }
        });

        Option[] watchOptions =
        [
            quietOption,
            verboseOption,
            listOption,
            noHotReloadOption,
            NonInteractiveOption
        ];

        // Options we need to know about that are passed through to the subcommand:
        var shortProjectOption = new Option<string>("-p") { Hidden = true, Arity = ArgumentArity.ZeroOrOne, AllowMultipleArgumentsPerToken = false };
        var longProjectOption = new Option<string>("--project") { Hidden = true, Arity = ArgumentArity.ZeroOrOne, AllowMultipleArgumentsPerToken = false };
        var launchProfileOption = new Option<string>("--launch-profile", "-lp") { Hidden = true, Arity = ArgumentArity.ZeroOrOne, AllowMultipleArgumentsPerToken = false };
        var noLaunchProfileOption = new Option<bool>("--no-launch-profile") { Hidden = true, Arity = ArgumentArity.Zero };

        //var binaryLogOption = new Option<string>(s_binaryLogOptionNames[0], [.. s_binaryLogOptionNames])
        //{
        //    Arity = ArgumentArity.ZeroOrOne,
        //    CustomParser = static r => r.Tokens.FirstOrDefault()?.ToString() ?? ""
        //};

        var rootCommand = new RootCommand(Resources.Help)
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
        //rootCommand.Options.Add(binaryLogOption);

        // We process all tokens that do not match any of the above options
        // to find the subcommand (the first unmatched token preceding "--")
        // and all its options and arguments.
        rootCommand.TreatUnmatchedTokensAsErrors = false;

        // We parse the command line outside of the action since the action
        // might not be invoked in presence of unmatched tokens.
        // We just need to know if the root command was invoked to handle --help.
        var rootCommandInvoked = false;
        rootCommand.SetAction(parseResult => rootCommandInvoked = true);

        ParserConfiguration parseConfig = new()
        {
            // To match dotnet command line parsing (see https://github.com/dotnet/sdk/blob/4712b35b94f2ad672e69ec35097cf86fc16c2e5e/src/Cli/dotnet/Parser.cs#L169):
            EnablePosixBundling = false,
        };

        // parse without forwarded options first:
        var parseResult = rootCommand.Parse(args, parseConfig);
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
        parseResult = rootCommand.Parse(args, parseConfig);
        if (ReportErrors(parseResult, reporter))
        {
            errorCode = 1;
            return null;
        }

        // invoke to execute default actions for displaying help
        errorCode = parseResult.Invoke(new()
        {
            Output = output,
            Error = output
        });

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

        var commandArguments = GetCommandArguments(parseResult, watchOptions, explicitCommand, out var binLogToken, out var binLogPath);

        // We assume that forwarded options, if any, are intended for dotnet build.
        var buildArguments = buildOptions.Select(option => ((IForwardedOption)option).GetForwardingFunction()(parseResult)).SelectMany(args => args).ToList();

        if (binLogToken != null)
        {
            buildArguments.Add(binLogToken);
        }

        var targetFrameworkOption = (Option<string>?)buildOptions.SingleOrDefault(option => option.Name == "--framework");

        return new()
        {
            List = parseResult.GetValue(listOption),
            GlobalOptions = new()
            {
                Quiet = parseResult.GetValue(quietOption),
                NoHotReload = parseResult.GetValue(noHotReloadOption),
                NonInteractive = parseResult.GetValue(NonInteractiveOption),
                Verbose = parseResult.GetValue(verboseOption),
                BinaryLogPath = ParseBinaryLogFilePath(binLogPath),
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

    /// <summary>
    /// Parses the value of msbuild option `-binaryLogger[:[LogFile=]output.binlog[;ProjectImports={None,Embed,ZipFile}]]`.
    /// Emulates https://github.com/dotnet/msbuild/blob/7f69ea906c29f2478cc05423484ad185de66e124/src/Build/Logging/BinaryLogger/BinaryLogger.cs#L481.
    /// See https://github.com/dotnet/msbuild/issues/12256
    /// </summary>
    internal static string? ParseBinaryLogFilePath(string? value)
        => value switch
        {
            null => null,
            _ => (from parameter in value.Split(';', StringSplitOptions.RemoveEmptyEntries)
                  where !string.Equals(parameter, "ProjectImports=None", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(parameter, "ProjectImports=Embed", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(parameter, "ProjectImports=ZipFile", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(parameter, "OmitInitialInfo", StringComparison.OrdinalIgnoreCase)
                  let path = (parameter.StartsWith("LogFile=", StringComparison.OrdinalIgnoreCase) ? parameter["LogFile=".Length..] : parameter).Trim('"')
                  let pathWithExtension = path.EndsWith(".binlog", StringComparison.OrdinalIgnoreCase) ? path : $"{path}.binlog"
                  select pathWithExtension)
                 .LastOrDefault("msbuild.binlog")
        };

    private static IReadOnlyList<string> GetCommandArguments(
        ParseResult parseResult,
        IReadOnlyList<Option> watchOptions,
        Command? explicitCommand,
        out string? binLogToken,
        out string? binLogPath)
    {
        var arguments = new List<string>();
        binLogToken = null;
        binLogPath = null;

        foreach (var child in parseResult.CommandResult.Children)
        {
            var optionResult = (OptionResult)child;

            // skip watch options:
            if (!watchOptions.Contains(optionResult.Option))
            {
                if (optionResult.Option.Name.Equals("--interactive", StringComparison.Ordinal) && parseResult.GetValue(NonInteractiveOption))
                {
                    // skip forwarding the interactive token (which may be computed by default) when users pass --non-interactive to watch itself
                    continue;
                }

                // skip Option<bool> zero-arity options with an implicit optionresult - these weren't actually specified by the user:
                if (optionResult.Option is Option<bool> boolOpt && boolOpt.Arity.Equals(ArgumentArity.Zero) && optionResult.Implicit)
                {
                    continue;
                }

                var optionNameToForward = GetOptionNameToForward(optionResult);
                if (optionResult.Tokens.Count == 0 && !optionResult.Implicit)
                {
                    arguments.Add(optionNameToForward);
                }
                else
                {
                    foreach (var token in optionResult.Tokens)
                    {
                        arguments.Add(optionNameToForward);
                        arguments.Add(token.Value);
                    }
                }
            }
        }

        // Assuming that all tokens after "--" are unmatched:
        var dashDashIndex = IndexOf(parseResult.Tokens, t => t.Value == "--");
        var unmatchedTokensBeforeDashDash = parseResult.UnmatchedTokens.Count - (dashDashIndex >= 0 ? parseResult.Tokens.Count - dashDashIndex - 1 : 0);

        var seenCommand = false;
        var dashDashInserted = false;

        for (int i = 0; i < parseResult.UnmatchedTokens.Count; i++)
        {
            var token = parseResult.UnmatchedTokens[i];

            if (i < unmatchedTokensBeforeDashDash)
            {
                if (!seenCommand && token == explicitCommand?.Name)
                {
                    seenCommand = true;
                    continue;
                }

                // Workaround: commands do not have forwarding option for -bl
                // https://github.com/dotnet/sdk/issues/49989
                foreach (var name in s_binaryLogOptionNames)
                {
                    if (token.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                    {
                        if (token.Length == name.Length)
                        {
                            binLogToken = token;
                            binLogPath = "";
                        }
                        else if (token.Length > name.Length + 1 && token[name.Length] == ':')
                        {
                            binLogToken = token;
                            binLogPath = token[(name.Length + 1)..];
                        }
                    }
                }
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

    private static string GetOptionNameToForward(OptionResult optionResult)
        // Some options _may_ be computed or have defaults, so not all may have an IdentifierToken.
        // For those that do not, use the Option's Name instead.
        => optionResult.IdentifierToken?.Value ?? optionResult.Option.Name;

    private static Command? TryGetSubcommand(ParseResult parseResult)
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
