// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Clean;

public sealed class CleanCommand : MSBuildForwardingApp
{
    public CleanCommand(MSBuildArgs msbuildArgs, string? msbuildPath = null)
        : base(msbuildArgs, msbuildPath)
    {
    }

    internal CleanCommand(
        MSBuildArgs msbuildArgs,
        ILLMEnvironmentDetector llmEnvironmentDetector,
        string? msbuildPath = null)
        : base(msbuildArgs, msbuildPath, llmEnvironmentDetector)
    {
    }

    public static CommandBase FromArgs(string[] args, string? msbuildPath = null)
    {
        var result = Parser.Parse(["dotnet", "clean", .. args]);
        return FromParseResult(result, msbuildPath);
    }

    internal static CommandBase FromArgs(
        string[] args,
        ILLMEnvironmentDetector llmEnvironmentDetector,
        string? msbuildPath = null)
    {
        var result = Parser.Parse(["dotnet", "clean", .. args]);
        return FromParseResult(result, llmEnvironmentDetector, msbuildPath);
    }

    public static CommandBase FromParseResult(ParseResult result, string? msbuildPath = null)
        => FromParseResult(result, new LLMEnvironmentDetectorForTelemetry(), msbuildPath);

    internal static CommandBase FromParseResult(
        ParseResult result,
        ILLMEnvironmentDetector llmEnvironmentDetector,
        string? msbuildPath = null)
    {
        var definition = (CleanCommandDefinition)result.CommandResult.Command;

        result.ShowHelpOrErrorIfAppropriate();
        return DotNetCommandFactory.CreateVirtualOrPhysicalCommand(
            definition,
            definition.SlnOrProjectOrFileArgument,
            createVirtualCommand: static (msbuildArgs, appFilePath) => new VirtualProjectBuildingCommand(
                entryPointFileFullPath: appFilePath,
                msbuildArgs: msbuildArgs)
            {
                NoBuild = false,
                NoRestore = true,
                NoCache = true,
                NoWriteBuildMarkers = true,
            },
            createPhysicalCommand: (msbuildArgs, msbuildPath) => new CleanCommand(msbuildArgs, llmEnvironmentDetector, msbuildPath),
            optionsToUseWhenParsingMSBuildFlags:
            [
                CommonOptions.CreatePropertyOption(),
                CommonOptions.CreateRestorePropertyOption(),
                CleanCommandDefinition.CreateTargetOption(),
                CommonOptions.CreateVerbosityOption(VerbosityOptions.normal),
                CommonOptions.CreateNoLogoOption()
            ],
            result,
            msbuildPath
        );
    }

    public static int Run(ParseResult parseResult)
    {
        parseResult.HandleDebugSwitch();
        return FromParseResult(parseResult).Execute();
    }
}
