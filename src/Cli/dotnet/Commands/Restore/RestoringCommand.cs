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
    public static ReadOnlyDictionary<string, string> RestoreOptimizationProperties =>
        new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { Constants.EnableDefaultItems, "false" },
            { Constants.EnableDefaultEmbeddedResourceItems, "false" },
            { Constants.EnableDefaultNoneItems, "false" },
        });

    public MSBuildForwardingApp? SeparateRestoreCommand { get; }

    private readonly bool AdvertiseWorkloadUpdates;

    public RestoringCommand(
        MSBuildArgs msbuildArgs,
        bool noRestore,
        string? msbuildPath = null,
        string? userProfileDir = null,
        bool? advertiseWorkloadUpdates = null)
        : base(GetCommandArguments(msbuildArgs, noRestore),  msbuildPath)
    {
        userProfileDir = CliFolderPathCalculator.DotnetUserProfileFolderPath;
        Task.Run(() => WorkloadManifestUpdater.BackgroundUpdateAdvertisingManifestsAsync(userProfileDir));
        SeparateRestoreCommand = GetSeparateRestoreCommand(msbuildArgs, noRestore, msbuildPath);
        AdvertiseWorkloadUpdates = advertiseWorkloadUpdates ?? msbuildArgs.OtherMSBuildArgs.All(arg => FlagsThatTriggerSilentRestore.All(f => !arg.Contains(f, StringComparison.OrdinalIgnoreCase)));

        if (!noRestore)
        {
            NuGetSignatureVerificationEnabler.ConditionallyEnable(this);
        }
    }

    /// <summary>
    /// Inspects and potentially modifies the MSBuildArgs structure for this command based on
    /// if this command needs to run a separate restore command or if it can be done as part of
    /// the same MSBuild invocation.
    ///
    /// If the command doesn't do a restore, no modifications are made.
    /// If the command requires a separate restore, we remove the MSBuild logo/header from this command.
    /// If the command is doing an inline restore, we need to ensure the restore-only
    /// properties are set correctly so that the restore operation uses our optimizations,
    /// while also getting the same set of properties as the build operation.
    /// </summary>
    private static MSBuildArgs GetCommandArguments(
        MSBuildArgs msbuildArgs,
        bool noRestore)
    {
        // if no restore will occur, then we're just running a normal build
        if (noRestore)
        {
            return msbuildArgs;
        }

        // if there are properties that we want to exclude from restore, we need to run a separate restore command
        // as a result, make this not emit MSBuild's header so that it doesn't look to end users like
        // we're running two separate build operations
        if (HasPropertyToExcludeFromRestore(msbuildArgs))
        {
            if (!msbuildArgs.OtherMSBuildArgs.Contains("-nologo"))
            {
                msbuildArgs.OtherMSBuildArgs.Add("-nologo");
            }
            return msbuildArgs;
        }

        // otherwise we're going to run an inline restore. In this case, we need to make sure that the restore properties
        // get initialized with the actual MSBuild properties (-rp is exclusively used by Restore if any -rp are present, so
        // we need to duplicate the -p's to ensure a consistent restore environment)
        msbuildArgs.ApplyPropertiesToRestore();
        msbuildArgs.OtherMSBuildArgs.Add("-restore");
        return msbuildArgs.CloneWithAdditionalRestoreProperties(RestoreOptimizationProperties);
    }

    /// <summary>
    /// Creates the separate restore command if needed.
    /// If no restore is requested, or if there are no properties that would trigger a separate restore,
    /// then this method returns null.
    /// If a separate restore command is needed, it returns an instance of <see cref="MSBuildForwardingApp"/>
    /// that is configured to run the restore operation with the appropriate properties and arguments.
    /// Because the separate restore command is run in a separate process,
    /// we don't have to map _restore_ properties - we can just use the regular properties.
    /// </summary>
    private static MSBuildForwardingApp? GetSeparateRestoreCommand(
        MSBuildArgs msbuildArgs,
        bool noRestore,
        string? msbuildPath)
    {
        // if the user asked for no restores, or there are no properties that would trigger a separate restore,
        // then we don't need to create a separate restore command. This is mututally exclusive with the similar
        // but opposite check in GetCommandArguments.
        if (noRestore || !HasPropertyToExcludeFromRestore(msbuildArgs))
        {
            return null;
        }

        // otherwise, we know we are creating a separate restore command.
        // we don't set the 'restore properties' of the MSBuildArgs, because
        // we are running a separate restore command - it can just use 'properties' instead.
        (var newArgumentsToAdd, var existingArgumentsToForward) = ProcessForwardedArgumentsForSeparateRestore(msbuildArgs);
        // we need to strip the properties from GlobalProperties that are excluded from restore
        // and create a new MSBuildArgs instance that will be used for the separate restore command
        ReadOnlyDictionary<string, string> restoreProperties =
            msbuildArgs.GlobalProperties?
            .Where(kvp => !IsPropertyExcludedFromRestore(kvp.Key))?
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase) is { } filteredList ? new(filteredList): ReadOnlyDictionary<string, string>.Empty;
        var restoreMSBuildArgs =
            MSBuildArgs.FromProperties(RestoreOptimizationProperties)
                       .CloneWithAdditionalTarget("Restore")
                       .CloneWithExplicitArgs([.. newArgumentsToAdd, .. existingArgumentsToForward])
                       .CloneWithAdditionalProperties(restoreProperties);
        if (msbuildArgs.Verbosity is {} verbosity)
        {
            restoreMSBuildArgs = restoreMSBuildArgs.CloneWithVerbosity(verbosity);
        }
        return RestoreCommand.CreateForwarding(restoreMSBuildArgs, msbuildPath);
    }

    private static bool HasPropertyToExcludeFromRestore(MSBuildArgs msbuildArgs)
        => msbuildArgs.GlobalProperties?.Keys.Any(IsPropertyExcludedFromRestore) ?? false;

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

    private static readonly List<string> PropertiesToExcludeFromSeparateRestore = [ .. PropertiesToExcludeFromRestore ];

    /// <summary>
    /// We investigate the arguments we're about to send to a separate restore call and filter out
    /// arguments that negatively influence the restore. In addition, some flags signal different modes of execution
    /// that we need to compensate for, so we might yield new arguments that should be  included in the overall restore call.
    /// </summary>
    private static (string[] newArgumentsToAdd, string[] existingArgumentsToForward) ProcessForwardedArgumentsForSeparateRestore(MSBuildArgs msbuildArgs)
    {
        // Separate restore should be silent in terminal logger - regardless of actual scenario
        HashSet<string> newArgumentsToAdd = ["-tlp:verbosity=quiet"];
        List<string> existingArgumentsToForward = [];
        bool hasSetNologo = false;

        foreach (var argument in msbuildArgs.OtherMSBuildArgs ?? [])
        {
            if (!IsExcludedFromSeparateRestore(argument))
            {
                if (argument.Equals("-nologo", StringComparison.OrdinalIgnoreCase))
                {
                    hasSetNologo = true;
                }
                existingArgumentsToForward.Add(argument);
            }

            if (TriggersSilentSeparateRestore(argument))
            {
                if (!hasSetNologo)
                {
                    newArgumentsToAdd.Add("-nologo");
                    hasSetNologo = true;
                }
                newArgumentsToAdd.Add("--verbosity:quiet");
            }
        }
        return (newArgumentsToAdd.ToArray(), existingArgumentsToForward.ToArray());
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

    private static bool IsPropertyExcludedFromRestore(string propertyName)
        => PropertiesToExcludeFromSeparateRestore.Contains(propertyName);

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
