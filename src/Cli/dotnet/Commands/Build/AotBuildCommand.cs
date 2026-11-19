// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if CLI_AOT
using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Commands.Restore;

namespace Microsoft.DotNet.Cli.Commands.Build;

internal static class AotBuildCommand
{
    internal static void ConfigureCommand(BuildCommandDefinition command) => command.SetAction(Execute);

    internal static int Execute(ParseResult parseResult) => Execute(parseResult, RunBuild);

    internal static int Execute(ParseResult parseResult, Func<ParseResult, int> runBuild, string? currentDirectory = null)
    {
        var definition = (BuildCommandDefinition)parseResult.CommandResult.Command;
        Option[] supported =
        [
            definition.OutputOption,
            definition.NoIncrementalOption,
            definition.NoDependenciesOption,
            definition.NoLogoOption,
            definition.NoRestoreOption,
            definition.SelfContainedOption,
            definition.NoSelfContainedOption,
            definition.FrameworkOption,
            definition.ConfigurationOption,
            definition.TargetOption,
            definition.VerbosityOption,
            definition.VersionSuffixOption,
            definition.InteractiveOption,
            definition.ArtifactsPathOption,
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
        if (!AotMSBuildServerCommand.IsEligible(
            parseResult, supported, parseResult.GetValue(definition.SlnOrProjectOrFileArgument) ?? [],
            currentDirectory ?? Environment.CurrentDirectory))
        {
            throw new CommandNotAvailableInAotException();
        }

        return runBuild(parseResult);
    }

    private static int RunBuild(ParseResult parseResult)
    {
        var command = (RestoringCommand)BuildCommand.FromParseResult(
            parseResult, msbuildPath: null, CommandServices.CreateDefault());
        AotMSBuildServerCommand.ConfigureServerClient(command, "build");
        return command.Execute();
    }
}
#endif
