// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Frozen;
using System.Collections.ObjectModel;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;

namespace Microsoft.DotNet.Cli.Commands.Restore;

public class RestoringCommand : MSBuildForwardingApp
{

    /// <summary>
    /// This dictionary contains properties that are set to disable the default items
    /// that are added to the project by default. These Item types are not needed
    /// during Restore, and can often cause performance issues by globbing across the
    /// entire workspace.
    /// </summary>
    public static FrozenDictionary<string, string> RestoreOptimizationProperties =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { Constants.EnableDefaultItems, "false" },
            { Constants.EnableDefaultEmbeddedResourceItems, "false" },
            { Constants.EnableDefaultNoneItems, "false" },
        }.ToFrozenDictionary();

    public MSBuildForwardingApp? SeparateRestoreCommand { get; }

    private readonly bool AdvertiseWorkloadUpdates;

    public RestoringCommand(
        IEnumerable<string> msbuildArgs,
        bool noRestore,
        FrozenDictionary<string, string>? restoreProperties,
        string? msbuildPath = null,
        string? userProfileDir = null,
        bool? advertiseWorkloadUpdates = null)
        : base(GetCommandArguments(msbuildArgs, noRestore, restoreProperties), msbuildPath)
    {
        userProfileDir = CliFolderPathCalculator.DotnetUserProfileFolderPath;
        Task.Run(() => WorkloadManifestUpdater.BackgroundUpdateAdvertisingManifestsAsync(userProfileDir));
        SeparateRestoreCommand = GetSeparateRestoreCommand(msbuildArgs, noRestore, msbuildPath, restoreProperties);
        AdvertiseWorkloadUpdates = advertiseWorkloadUpdates ?? msbuildArgs.All(arg => FlagsThatTriggerSilentRestore.All(f => !arg.Contains(f, StringComparison.OrdinalIgnoreCase)));

        if (!noRestore)
        {
            NuGetSignatureVerificationEnabler.ConditionallyEnable(this);
        }
    }

    private static IEnumerable<string> GetCommandArguments(
        IEnumerable<string> arguments,
        bool noRestore,
        FrozenDictionary<string, string>? restoreProperties)
    {
        if (noRestore)
        {
            return arguments;
        }

        if (HasArgumentToExcludeFromRestore(arguments))
        {
            return ["-nologo", ..arguments];
        }

        return ["-restore", .. arguments, ..MapRestoreProperties(restoreProperties)];
    }

    private static List<string> MapRestoreProperties(FrozenDictionary<string, string>? restoreProperties) => [
            ..RestoreOptimizationProperties.Select(kvp => $"--restoreProperty:{kvp.Key}={kvp.Value}"),
            // putting the user properties at the end so that they can override the defaults
            ..restoreProperties is null ? [] : restoreProperties.Select(kvp => $"--restoreProperty:{kvp.Key}={kvp.Value}")
        ];

    private static MSBuildForwardingApp? GetSeparateRestoreCommand(
        IEnumerable<string> arguments,
        bool noRestore,
        string? msbuildPath,
        FrozenDictionary<string, string>? restoreProperties)
    {
        if (noRestore || !HasArgumentToExcludeFromRestore(arguments))
        {
            return null;
        }

        (var newArgumentsToAdd, var existingArgumentsToForward) = ProcessForwardedArgumentsForSeparateRestore(arguments);
        string[] restoreArguments = ["--target:Restore", .. newArgumentsToAdd, .. existingArgumentsToForward, ..MapRestoreProperties(restoreProperties)];

        return RestoreCommand.CreateForwarding(restoreArguments, msbuildPath);
    }

    private static bool HasArgumentToExcludeFromRestore(IEnumerable<string> arguments)
        => arguments.Any(a => IsExcludedFromRestore(a));

    private static readonly string[] switchPrefixes = ["-", "/", "--"];

    /// <summary>
    /// these properties trigger a separate restore
    /// </summary>
    private static readonly string[] PropertiesToExcludeFromRestore =
    [
        "TargetFramework"
    ];

    /// <summary>
    ///  These arguments should lead to absolutely no output from the restore command
    /// </summary>
    private static readonly string[] FlagsThatTriggerSilentRestore =
    [
        "getProperty",
        "getItem",
        "getTargetResult"
    ];

    /// <summary>
    ///  These arguments don't by themselves require that restore be run in a separate process,
    ///  but if there is a separate restore process they shouldn't be passed to it
    /// </summary>
    private static readonly string[] FlagsToExcludeFromRestore =
    [
        ..FlagsThatTriggerSilentRestore,
        "t",
        "target",
        "consoleloggerparameters",
        "clp"
    ];

    private static readonly List<string> FlagsToExcludeFromSeparateRestore = [.. ComputeFlags(FlagsToExcludeFromRestore)];

    private static readonly List<string> FlagsThatTriggerSilentSeparateRestore = [.. ComputeFlags(FlagsThatTriggerSilentRestore)];

    private static readonly List<string> PropertiesToExcludeFromSeparateRestore = [.. ComputePropertySwitches(PropertiesToExcludeFromRestore)];

    /// <summary>
    /// We investigate the arguments we're about to send to a separate restore call and filter out
    /// arguments that negatively influence the restore. In addition, some flags signal different modes of execution
    /// that we need to compensate for, so we might yield new arguments that should be included in the overall restore call.
    /// </summary>
    /// <param name="forwardedArguments"></param>
    /// <returns></returns>
    private static (string[] newArgumentsToAdd, string[] existingArgumentsToForward) ProcessForwardedArgumentsForSeparateRestore(IEnumerable<string> forwardedArguments)
    {
        // Separate restore should be silent in terminal logger - regardless of actual scenario
        HashSet<string> newArgumentsToAdd = ["-tlp:verbosity=quiet"];
        List<string> existingArgumentsToForward = [];

        foreach (var argument in forwardedArguments ?? [])
        {
            if (!IsExcludedFromSeparateRestore(argument) && !IsExcludedFromRestore(argument))
            {
                existingArgumentsToForward.Add(argument);
            }

            if (TriggersSilentSeparateRestore(argument))
            {
                newArgumentsToAdd.Add("-nologo");
                newArgumentsToAdd.Add("-verbosity:quiet");
            }
        }
        return (newArgumentsToAdd.ToArray(), existingArgumentsToForward.ToArray());
    }
    private static IEnumerable<string> ComputePropertySwitches(string[] properties)
    {
        foreach (var prefix in switchPrefixes)
        {
            foreach (var property in properties)
            {
                yield return $"{prefix}property:{property}=";
                yield return $"{prefix}p:{property}=";
            }
        }
    }

    private static IEnumerable<string> ComputeFlags(string[] flags)
    {
        foreach (var prefix in switchPrefixes)
        {
            foreach (var flag in flags)
            {
                yield return $"{prefix}{flag}:";
            }
        }
    }

    private static bool IsExcludedFromRestore(string argument)
        => PropertiesToExcludeFromSeparateRestore.Any(flag => argument.StartsWith(flag, StringComparison.OrdinalIgnoreCase));


    private static bool IsExcludedFromSeparateRestore(string argument)
        => FlagsToExcludeFromSeparateRestore.Any(p => argument.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    // These arguments should lead to absolutely no output from the restore command - regardless of loggers
    private static bool TriggersSilentSeparateRestore(string argument)
        => FlagsThatTriggerSilentSeparateRestore.Any(p => argument.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    public override int Execute()
    {
        int exitCode;
        if (SeparateRestoreCommand != null)
        {
            exitCode = SeparateRestoreCommand.Execute();
            if (exitCode != 0)
            {
                return exitCode;
            }
        }

        exitCode = base.Execute();
        if (AdvertiseWorkloadUpdates)
        {
            WorkloadManifestUpdater.AdvertiseWorkloadUpdates();
        }
        return exitCode;
    }
}
