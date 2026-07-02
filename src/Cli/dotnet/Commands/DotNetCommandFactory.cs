// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.CommandFactory;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.DotNet.ProjectTools;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli;

public class DotNetCommandFactory(bool alwaysRunOutOfProc = false, string? currentWorkingDirectory = null) : ICommandFactory
{
    private readonly bool _alwaysRunOutOfProc = alwaysRunOutOfProc;
    private readonly string? _currentWorkingDirectory = currentWorkingDirectory;

    public ICommand Create(string commandName, IEnumerable<string> args, NuGetFramework? framework = null, string configuration = Constants.DefaultConfiguration)
    {
        if (!_alwaysRunOutOfProc && TryGetBuiltInCommand(commandName, out var builtInCommand))
        {
            Debug.Assert(framework == null, "BuiltInCommand doesn't support the 'framework' argument.");
            Debug.Assert(configuration == Constants.DefaultConfiguration, "BuiltInCommand doesn't support the 'configuration' argument.");

            return new BuiltInCommand(commandName, args, builtInCommand);
        }

        return CommandFactoryUsingResolver.CreateDotNet(commandName, args, framework, configuration, _currentWorkingDirectory);
    }

    private static bool TryGetBuiltInCommand(string commandName, out Func<string[], int> commandFunc)
    {
        var command = Parser.GetBuiltInCommand(commandName);
        if (command?.Action is AsynchronousCommandLineAction action)
        {
            commandFunc = (args) => Parser.Invoke([commandName, .. args]);
            return true;
        }
        // No-op delegate for failure case.
        commandFunc = (args) => 1;
        return false;
    }

    internal static CommandBase CreateVirtualOrPhysicalCommand(
        System.CommandLine.Command commandDefinition,
        Argument<string[]> catchAllUserInputArgument,
        Func<MSBuildArgs, string, VirtualProjectBuildingCommand> createVirtualCommand,
        Func<MSBuildArgs, string?, CommandBase> createPhysicalCommand,
        IEnumerable<Option> optionsToUseWhenParsingMSBuildFlags,
        ParseResult parseResult,
        string? msbuildPath = null,
        Func<MSBuildArgs, ImmutableArray<string>, MSBuildArgs>? transformer = null)
    {
        var args = parseResult.GetValue(catchAllUserInputArgument) ?? [];
        LoggerUtility.SeparateLoggerArguments(args, out var loggerArgs, out var nonLoggerArgs);
        var forwardedArgs = parseResult.OptionValuesToBeForwarded(commandDefinition);
        if (nonLoggerArgs is [{ } arg] && VirtualProjectBuilder.IsValidEntryPointPath(arg))
        {
            if (RuntimeFeature.IsDynamicCodeSupported)
            {
                var msbuildArgs = MSBuildArgs.AnalyzeMSBuildArguments([.. forwardedArgs, .. loggerArgs],
                [
                    .. optionsToUseWhenParsingMSBuildFlags,
                    CommonOptions.CreateGetPropertyOption(),
                    CommonOptions.CreateGetItemOption(),
                    CommonOptions.CreateGetTargetResultOption(),
                    CommonOptions.CreateGetResultOutputFileOption(),
                ]);
                msbuildArgs = transformer?.Invoke(msbuildArgs, nonLoggerArgs) ?? msbuildArgs;
                return createVirtualCommand(msbuildArgs, Path.GetFullPath(arg));
            }
            else
            {
                throw new PlatformNotSupportedException("Dynamic code generation is not supported on this platform.");
            }
        }
        else
        {
            // Warn if any argument looks like a file-based program entry point but we're falling back to MSBuild.
            // This can happen when extra positional arguments prevent the single-arg file-based path from being taken,
            // or when a .cs file doesn't exist (so IsValidEntryPointPath returns false).
            foreach (var candidate in nonLoggerArgs)
            {
                if (VirtualProjectBuilder.IsValidEntryPointPath(candidate))
                {
                    Reporter.Error.WriteLine(
                        string.Format(
                            CliCommandStrings.WarningFileArgumentPassedToMSBuild,
                            candidate,
                            commandDefinition.Name,
                            FormatUnsupportedArguments(nonLoggerArgs, candidate)).Yellow());
                    break;
                }

                if (candidate.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    Reporter.Error.WriteLine(
                        string.Format(
                            CliCommandStrings.WarningCsFileArgumentPassedToMSBuild,
                            candidate,
                            FormatUnrecognizedArguments(nonLoggerArgs)).Yellow());
                    break;
                }
            }

            var msbuildArgs = MSBuildArgs.AnalyzeMSBuildArguments([.. forwardedArgs, .. args], [.. optionsToUseWhenParsingMSBuildFlags]);
            msbuildArgs = transformer?.Invoke(msbuildArgs, nonLoggerArgs) ?? msbuildArgs;
            return createPhysicalCommand(msbuildArgs, msbuildPath);
        }
    }

    private static string FormatUnsupportedArguments(IEnumerable<string> args, string supportedArgument) =>
        FormatArguments(RemoveFirst(args, supportedArgument));

    private static string FormatUnrecognizedArguments(IEnumerable<string> args) => FormatArguments(args);

    private static string FormatArguments(IEnumerable<string> args) => string.Join(", ", args.Select(arg => $"'{arg}'"));

    private static IEnumerable<string> RemoveFirst(IEnumerable<string> args, string value)
    {
        var removed = false;
        foreach (var arg in args)
        {
            if (!removed && string.Equals(arg, value, StringComparison.Ordinal))
            {
                removed = true;
                continue;
            }

            yield return arg;
        }
    }
}
