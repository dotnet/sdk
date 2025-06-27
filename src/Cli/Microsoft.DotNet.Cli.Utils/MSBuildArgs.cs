// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Frozen;
using System.CommandLine;

namespace Microsoft.DotNet.Cli.Utils;

/// <summary>
/// Represents all of the parsed and forwarded arguments that the SDK should pass to MSBuild.
/// </summary>
public sealed class MSBuildArgs
{
    private MSBuildArgs(FrozenDictionary<string, string>? properties, FrozenDictionary<string, string>? restoreProperties, string[]? targets, string[]? otherMSBuildArgs)
    {
        GlobalProperties = properties;
        RestoreGlobalProperties = restoreProperties;
        RequestedTargets = targets;
        OtherMSBuildArgs = otherMSBuildArgs is not null
            ? [.. otherMSBuildArgs]
            : new List<string>();
    }

    /// <summary>
    /// The set of <c>-p</c> flags that should be passed to MSBuild.
    /// </summary>
    public FrozenDictionary<string, string>? GlobalProperties { get; }
    /// <summary>
    /// The set of <c>-rp</c> flags that should be passed to MSBuild for restore operations only.
    /// If this is non-empty, all <see cref="GlobalProperties"/> flags should be passed as <c>-rp</c> as well.
    /// </summary>
    public FrozenDictionary<string, string>? RestoreGlobalProperties { get; private set; }

    /// <summary>
    /// The ordered list of targets that should be passed to MSBuild.
    /// </summary>
    public string[]? RequestedTargets { get; }

    /// <summary>
    /// All non <c>-p</c> and <c>-rp</c> arguments that should be passed to MSBuild.
    /// </summary>
    public List<string> OtherMSBuildArgs { get; }

    /// <summary>
    /// Takes all of the unstructured properties and arguments that have been accrued from the command line
    /// processing of the SDK and returns a structured set of MSBuild arguments grouped by purpose.
    /// </summary>
    /// <param name="forwardedAndUserFacingArgs">the complete set of forwarded MSBuild arguments and un-parsed, potentially MSBuild-relevant arguments</param>
    /// <returns></returns>
    public static MSBuildArgs AnalyzeMSBuildArguments(IEnumerable<string> forwardedAndUserFacingArgs, Option<FrozenDictionary<string, string>?> propertiesOption, Option<FrozenDictionary<string, string>?> restorePropertiesOption, Option<string[]?>? targetsOption)
    {
        var fakeCommand = new System.CommandLine.Command("dotnet") {
            propertiesOption,
            restorePropertiesOption,
        };
        if (targetsOption is not null)
        {
            fakeCommand.Options.Add(targetsOption);
        }
        var propertyParsingConfiguration = new CommandLineConfiguration(fakeCommand)
        {
            EnablePosixBundling = false
        };
        var parseResult = propertyParsingConfiguration.Parse([..forwardedAndUserFacingArgs]);
        var globalProperties = parseResult.GetValue(propertiesOption);
        var restoreProperties = parseResult.GetValue(restorePropertiesOption);
        var requestedTargets = targetsOption is not null ? parseResult.GetValue(targetsOption) : null;
        var otherMSBuildArgs = parseResult.UnmatchedTokens.ToArray();
        return new MSBuildArgs(
            properties: globalProperties,
            restoreProperties: restoreProperties,
            targets: requestedTargets,
            otherMSBuildArgs: otherMSBuildArgs);
    }

    public static MSBuildArgs FromProperties(FrozenDictionary<string, string>? properties)
    {
        return new MSBuildArgs(properties, null, null, null);
    }

    public static MSBuildArgs FromOtherArgs(params ReadOnlySpan<string> args)
    {
        return new MSBuildArgs(null, null, null, args.ToArray());
    }

    public static readonly MSBuildArgs ForHelp = new(null, null, null, ["--help"]);

    public MSBuildArgs CloneWithExplicitArgs(string[] newArgs)
    {
        return new MSBuildArgs(
            properties: GlobalProperties,
            restoreProperties: RestoreGlobalProperties,
            targets: RequestedTargets,
            otherMSBuildArgs: newArgs);
    }

    public MSBuildArgs CloneWithAdditionalRestoreProperties(FrozenDictionary<string, string>? additionalRestoreProperties)
    {
        if (additionalRestoreProperties is null || additionalRestoreProperties.Count == 0)
        {
            // If there are no additional restore properties, we can just return the current instance.
            return new MSBuildArgs(GlobalProperties, RestoreGlobalProperties, RequestedTargets, OtherMSBuildArgs.ToArray());
        }
        if (RestoreGlobalProperties is null)
        {
            return new MSBuildArgs(GlobalProperties, additionalRestoreProperties, RequestedTargets, OtherMSBuildArgs.ToArray());
        }

        var newRestoreProperties = new Dictionary<string, string>(RestoreGlobalProperties, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in additionalRestoreProperties)
        {
            newRestoreProperties[kvp.Key] = kvp.Value;
        }
        return new MSBuildArgs(GlobalProperties, newRestoreProperties.ToFrozenDictionary(newRestoreProperties.Comparer), RequestedTargets, OtherMSBuildArgs.ToArray());
    }

    public MSBuildArgs CloneWithAdditionalProperties(FrozenDictionary<string, string>? additionalProperties)
    {
        if (additionalProperties is null || additionalProperties.Count == 0)
        {
            // If there are no additional properties, we can just return the current instance.
            return new MSBuildArgs(GlobalProperties, RestoreGlobalProperties, RequestedTargets, OtherMSBuildArgs.ToArray());
        }
        if (GlobalProperties is null)
        {
            return new MSBuildArgs(additionalProperties, RestoreGlobalProperties, RequestedTargets, OtherMSBuildArgs.ToArray());
        }

        var newProperties = new Dictionary<string, string>(GlobalProperties, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in additionalProperties)
        {
            newProperties[kvp.Key] = kvp.Value;
        }
        return new MSBuildArgs(newProperties.ToFrozenDictionary(newProperties.Comparer), RestoreGlobalProperties, RequestedTargets, OtherMSBuildArgs.ToArray());
    }

    public MSBuildArgs CloneWithAdditionalTarget(string additionalTarget)
    {
        string[] newTargets = RequestedTargets is not null
            ? [.. RequestedTargets, additionalTarget]
            : [ additionalTarget ];
        return new MSBuildArgs(GlobalProperties, RestoreGlobalProperties, newTargets, OtherMSBuildArgs.ToArray());
    }

    public void ApplyPropertiesToRestore()
    {
        if (RestoreGlobalProperties is null)
        {
            RestoreGlobalProperties = GlobalProperties;
            return;
        }
        else if (GlobalProperties is not null && GlobalProperties.Count > 0)
        {
            // If we have restore properties, we need to merge the global properties into them.
            // We ensure the restore properties overwrite the properties to align with expected MSBuild semantics.
            var newdict = new Dictionary<string, string>(GlobalProperties, StringComparer.OrdinalIgnoreCase);
            foreach (var restoreKvp in RestoreGlobalProperties)
            {
                newdict[restoreKvp.Key] = restoreKvp.Value;
            }
            RestoreGlobalProperties = newdict.ToFrozenDictionary(newdict.Comparer);
        }
    }
}
