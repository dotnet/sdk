// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Data;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Build;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal sealed class CommandLineOptions
{
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

    public required string Command { get; init; }

    public required bool IsExplicitCommand { get; init; }

    public static CommandLineOptions? Parse(IReadOnlyList<string> args, ILogger logger, TextWriter output, out int errorCode)
    {
        var definition = new DotnetWatchCommandDefinition();

        // We parse the command line outside of the action since the action
        // might not be invoked in presence of unmatched tokens.
        // We just need to know if the root command was invoked to handle --help.
        var rootCommandInvoked = false;
        definition.SetAction(parseResult => rootCommandInvoked = true);

        ParserConfiguration parseConfig = new()
        {
            // To match dotnet command line parsing (see https://github.com/dotnet/sdk/blob/4712b35b94f2ad672e69ec35097cf86fc16c2e5e/src/Cli/dotnet/Parser.cs#L169):
            EnablePosixBundling = false,
        };

        // parse without forwarded options first:
        var parseResult = definition.Parse(args, parseConfig);
        if (ReportErrors(parseResult, logger))
        {
            errorCode = 1;
            return null;
        }

        // determine subcommand:
        var command = GetSubcommand(parseResult, out bool isExplicitCommand);
        var buildOptions = command.Options.Where(o => o.ForwardingFunction is not null);

        foreach (var buildOption in buildOptions)
        {
            definition.Options.Add(buildOption);
        }

        // reparse with forwarded options:
        parseResult = definition.Parse(args, parseConfig);
        if (ReportErrors(parseResult, logger))
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

        var projectValue = parseResult.GetValue(definition.LongProjectOption);
        if (string.IsNullOrEmpty(projectValue))
        {
            var projectShortValue = parseResult.GetValue(definition.ShortProjectOption);
            if (!string.IsNullOrEmpty(projectShortValue))
            {
                logger.LogWarning(Resources.Warning_ProjectAbbreviationDeprecated);
                projectValue = projectShortValue;
            }
        }

        var commandArguments = GetCommandArguments(
            parseResult,
            command,
            isExplicitCommand,
            out var binLogToken,
            out var binLogPath);

        // We assume that forwarded options, if any, are intended for dotnet build.
        var buildArguments = buildOptions.Select(option => option.ForwardingFunction!(parseResult)).SelectMany(args => args).ToList();

        if (binLogToken != null)
        {
            buildArguments.Add(binLogToken);
        }

        var targetFrameworkOption = (Option<string>?)buildOptions.SingleOrDefault(option => option.Name == "--framework");

        var logLevel = parseResult.GetValue(definition.VerboseOption)
            ? LogLevel.Debug
            : parseResult.GetValue(definition.QuietOption)
            ? LogLevel.Warning
            : LogLevel.Information;

        return new()
        {
            List = parseResult.GetValue(definition.ListOption),
            GlobalOptions = new()
            {
                LogLevel = logLevel,
                NoHotReload = parseResult.GetValue(definition.NoHotReloadOption),
                NonInteractive = parseResult.GetValue(definition.NonInteractiveOption),
                BinaryLogPath = ParseBinaryLogFilePath(binLogPath),
            },

            CommandArguments = commandArguments,
            Command = command.Name,
            IsExplicitCommand = isExplicitCommand,

            ProjectPath = projectValue,
            LaunchProfileName = parseResult.GetValue(definition.LaunchProfileOption),
            NoLaunchProfile = parseResult.GetValue(definition.NoLaunchProfileOption),
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
        Command command,
        bool isExplicitCommand,
        out string? binLogToken,
        out string? binLogPath)
    {
        var definition = (DotnetWatchCommandDefinition)parseResult.CommandResult.Command;

        var arguments = new List<string>();
        binLogToken = null;
        binLogPath = null;

        foreach (var child in parseResult.CommandResult.Children)
        {
            var optionResult = (OptionResult)child;

            // skip watch specific option:
            if (definition.IsWatchOption(optionResult.Option))
            {
                continue;
            }
            
            // forward forwardable option if the subcommand supports it:
            if (!command.Options.Any(option => option.Name == optionResult.Option.Name))
            {
                // pass project as an argument to commands that do not have --project option but accept project as an argument:
                if (command is BuildCommandDefinition or TestCommandDefinition.VSTest &&
                    (optionResult.Option == definition.ShortProjectOption || optionResult.Option == definition.LongProjectOption) &&
                    parseResult.GetValue((Option<string>)optionResult.Option) is { } projectPath)
                {
                    arguments.Add(projectPath);
                }

                continue;
            }

            // skip forwarding the interactive token (which may be computed by default) when users pass --non-interactive to watch itself
            if (optionResult.Option == definition.NonInteractiveOption && parseResult.GetValue(definition.NonInteractiveOption))
            {
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
                if (!seenCommand && isExplicitCommand && token == command.Name)
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

    private static Command GetSubcommand(ParseResult parseResult, out bool isExplicit)
    {
        // Assuming that all tokens after "--" are unmatched:
        var dashDashIndex = IndexOf(parseResult.Tokens, t => t.Value == "--");
        var unmatchedTokensBeforeDashDash = parseResult.UnmatchedTokens.Count - (dashDashIndex >= 0 ? parseResult.Tokens.Count - dashDashIndex - 1 : 0);

        var dotnetDefinition = new DotNetCommandDefinition();
        var knownCommandsByName = dotnetDefinition.Subcommands.ToDictionary(keySelector: c => c.Name, elementSelector: c => c);

        for (int i = 0; i < unmatchedTokensBeforeDashDash; i++)
        {
            // command token can't follow "--"
            if (knownCommandsByName.TryGetValue(parseResult.UnmatchedTokens[i], out var explicitCommand))
            {
                isExplicit = true;
                return explicitCommand;
            }
        }

        isExplicit = false;
        return dotnetDefinition.RunCommand;
    }

    private static bool ReportErrors(ParseResult parseResult, ILogger logger)
    {
        if (parseResult.Errors.Any())
        {
            foreach (var error in parseResult.Errors)
            {
                logger.LogError(error.Message);
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
}
