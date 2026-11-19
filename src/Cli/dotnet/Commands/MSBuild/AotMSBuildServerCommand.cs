// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if CLI_AOT
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Security;
using Microsoft.DotNet.Cli.Commands.Clean;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Utils;
using CleanCommand = Microsoft.DotNet.Cli.Commands.Clean.CleanCommand;
using RestoreCommand = Microsoft.DotNet.Cli.Commands.Restore.RestoreCommand;

namespace Microsoft.DotNet.Cli.Commands.MSBuild;

internal static class AotMSBuildServerCommand
{
    private static readonly Option s_propertyOption = CommonOptions.CreatePropertyOption();

    internal static void ConfigureCommand(RestoreCommandDefinition command) => command.SetAction(ExecuteRestore);

    internal static void ConfigureCommand(CleanCommandDefinition command) => command.SetAction(ExecuteClean);

    private static int ExecuteRestore(ParseResult parseResult) => ExecuteRestore(parseResult, RunRestore);

    internal static int ExecuteRestore(ParseResult parseResult, Func<ParseResult, int> run, string? currentDirectory = null)
    {
        var definition = (RestoreCommandDefinition)parseResult.CommandResult.Command;
        Option[] supported =
        [
            definition.TargetOption,
            definition.VerbosityOption,
            definition.NoLogoOption,
            definition.NoDependenciesOption,
            definition.InteractiveOption,
            definition.ArtifactsPathOption,
            definition.UseLockFileOption,
            definition.LockedModeOption,
            definition.LockFilePathOption,
            definition.ForceEvaluateOption,
            definition.TargetPlatformOptions.RuntimeOption,
            definition.TargetPlatformOptions.ArchitectureOption,
            definition.TargetPlatformOptions.OperatingSystemOption,
            definition.ImplicitRestoreOptions.SourceOption,
            definition.ImplicitRestoreOptions.PackagesOption,
            definition.ImplicitRestoreOptions.CurrentRuntimeOption,
            definition.ImplicitRestoreOptions.DisableParallelOption,
            definition.ImplicitRestoreOptions.ConfigFileOption,
            definition.ImplicitRestoreOptions.NoCacheOption,
            definition.ImplicitRestoreOptions.NoHttpCacheOption,
            definition.ImplicitRestoreOptions.IgnoreFailedSourcesOption,
            definition.ImplicitRestoreOptions.ForceOption,
            definition.ImplicitRestoreOptions.PropertiesOption,
            definition.ImplicitRestoreOptions.RestorePropertiesOption,
        ];
        if (!IsEligible(parseResult, supported, parseResult.GetValue(definition.SlnOrProjectOrFileArgument) ?? [],
            currentDirectory ?? Environment.CurrentDirectory))
        {
            throw new CommandNotAvailableInAotException();
        }

        return run(parseResult);
    }

    private static int RunRestore(ParseResult parseResult)
    {
        var command = (MSBuildForwardingApp)RestoreCommand.FromParseResult(
            parseResult, msbuildPath: null, CommandServices.CreateDefault());
        ConfigureServerClient(command, "restore");
        return command.Execute();
    }

    private static int ExecuteClean(ParseResult parseResult) => ExecuteClean(parseResult, RunClean);

    internal static int ExecuteClean(ParseResult parseResult, Func<ParseResult, int> run, string? currentDirectory = null)
    {
        var definition = (CleanCommandDefinition)parseResult.CommandResult.Command;
        Option[] supported =
        [
            definition.OutputOption,
            definition.NoLogoOption,
            definition.FrameworkOption,
            definition.ConfigurationOption,
            definition.TargetOption,
            definition.VerbosityOption,
            definition.RuntimeOption,
            definition.InteractiveOption,
            definition.ArtifactsPathOption,
        ];
        if (!IsEligible(parseResult, supported, parseResult.GetValue(definition.SlnOrProjectOrFileArgument) ?? [],
            currentDirectory ?? Environment.CurrentDirectory, allowPropertyArguments: true))
        {
            throw new CommandNotAvailableInAotException();
        }

        return run(parseResult);
    }

    private static int RunClean(ParseResult parseResult)
    {
        var command = (MSBuildForwardingApp)CleanCommand.FromParseResult(
            parseResult, msbuildPath: null, CommandServices.CreateDefault());
        ConfigureServerClient(command, "clean");
        return command.Execute();
    }

    internal static void ConfigureServerClient(MSBuildForwardingApp command, string commandName)
    {
        if (string.IsNullOrEmpty(Env.GetEnvironmentVariable("MSBUILDFAILONDRIVEENUMERATINGWILDCARD")))
        {
            command.EnvironmentVariable("MSBUILDFAILONDRIVEENUMERATINGWILDCARD", "1");
        }

        command.UseMSBuildServerClient(commandName);
        if (command is RestoringCommand { SeparateRestoreCommand: { } restore })
        {
            ConfigureServerClient(restore, "restore");
        }
    }

    internal static bool IsEligible(
        ParseResult parseResult,
        IEnumerable<Option> supportedOptions,
        IEnumerable<string> operands,
        string currentDirectory,
        bool operandsAreUnmatchedTokens = false,
        bool allowPropertyArguments = false)
    {
        if (!IsServerEnabled()
            || Env.GetEnvironmentVariableAsBool("DOTNET_CLI_RUN_MSBUILD_OUTOFPROC")
            || !string.IsNullOrEmpty(Env.GetEnvironmentVariable("MSBUILD_EXE_PATH"))
            || !string.IsNullOrEmpty(Env.GetEnvironmentVariable("MSBUILD_LOGGING_ARGS"))
            || !string.IsNullOrEmpty(Env.GetEnvironmentVariable("MSBUILDDEBUGONSTART"))
            || Env.GetEnvironmentVariable("MSBUILDDUMPPROCESSCOUNTERS") == "1"
            || Env.GetEnvironmentVariable("MSBUILDENSURESTDOUTFORTASKPROCESSES") == "1"
            || Env.GetEnvironmentVariable("MSBUILDDISABLENODEREUSE") == "1"
            || Env.GetEnvironmentVariable("MSBUILDFORCEMULTITHREADED") == "1"
            || Env.GetEnvironmentVariable("MSBUILDENABLEMULTITHREADED") == "1"
            || parseResult.Errors.Count != 0
            || parseResult.RootCommandResult.Children.OfType<OptionResult>().Any(result => !result.Implicit)
            || parseResult.CommandResult.Children.OfType<OptionResult>()
                .Any(result => !result.Implicit && !supportedOptions.Contains(result.Option))
            || parseResult.Tokens.Any(token => token.Value == "--")
            || (!operandsAreUnmatchedTokens && parseResult.UnmatchedTokens.Any(token => !IsForwardedArgument(token, allowPropertyArguments)))
            || !HasCompatibleConsoleCulture())
        {
            return false;
        }

        string[] paths = operands.Where(arg => !IsForwardedArgument(arg, allowPropertyArguments)).ToArray();

        try
        {
            string projectPath;
            if (paths.Length == 0)
            {
                string[] candidates = Directory.GetFiles(currentDirectory)
                    .Where(IsProjectOrSolution)
                    .Take(2)
                    .ToArray();
                if (candidates.Length != 1)
                {
                    return false;
                }

                projectPath = candidates[0];
            }
            else if (paths.Length == 1 && !paths[0].StartsWith('-'))
            {
                projectPath = Path.GetFullPath(paths[0], currentDirectory);
                if (!IsProjectOrSolution(projectPath) || !File.Exists(projectPath))
                {
                    return false;
                }
            }
            else
            {
                return false;
            }

            if (File.Exists(Path.Combine(SdkPaths.SdkDirectory, "MSBuild.rsp")))
            {
                return false;
            }

            string projectDirectory = Path.GetDirectoryName(projectPath)!;
            if (File.Exists(Path.Combine(projectDirectory, "MSBuild.rsp")))
            {
                return false;
            }

            for (DirectoryInfo? directory = new(projectDirectory); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.rsp")))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or SecurityException)
        {
            return false;
        }
    }

    private static bool IsForwardedArgument(string argument, bool allowPropertyArguments)
    {
        if (LoggerUtility.IsBinLogArgument(argument))
        {
            return true;
        }

        int colon = argument.IndexOf(':');
        if (!allowPropertyArguments || colon <= 0 || argument.IndexOf('=', colon + 1) <= colon + 1)
        {
            return false;
        }

        string alias = argument[..colon];
        return s_propertyOption.Name == alias || s_propertyOption.Aliases.Contains(alias);
    }

    private static bool IsServerEnabled()
    {
        string? value = Env.GetEnvironmentVariable("MSBUILDUSESERVER");
        return string.IsNullOrEmpty(value)
            ? Env.GetEnvironmentVariableAsBool("DOTNET_CLI_USE_MSBUILD_SERVER", defaultValue: true)
            : value == "1";
    }

    private static bool IsProjectOrSolution(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.EndsWith("proj", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".slnf", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasCompatibleConsoleCulture()
    {
        CultureInfo culture = CultureInfo.CurrentUICulture;
        if (!culture.Equals(culture.GetConsoleFallbackUICulture()))
        {
            return false;
        }

        if (Env.GetEnvironmentVariable(EnvironmentVariableNames.DOTNET_CLI_CONSOLE_USE_DEFAULT_ENCODING) != "1"
            && UILanguageOverride.OperatingSystemSupportsUtf8())
        {
            return true;
        }

        try
        {
            int codePage = Console.OutputEncoding.CodePage;
            return codePage == 65001
                || codePage == culture.TextInfo.OEMCodePage
                || codePage == culture.TextInfo.ANSICodePage
                || culture.Equals(CultureInfo.InvariantCulture);
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }
}
#endif
