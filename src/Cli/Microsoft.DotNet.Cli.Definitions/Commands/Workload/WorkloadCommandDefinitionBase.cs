// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload;

internal abstract class WorkloadCommandDefinitionBase(string name, string description)
    : Command(name, description)
{
    public virtual Option<bool>? SkipSignCheckOption => null;
    public virtual Option<string>? TempDirOption => null;
    public virtual Option<Utils.VerbosityOptions>? VerbosityOption => null;
    public virtual NuGetRestoreOptions? RestoreOptions => null;

    public static Option<string> CreateSdkVersionOption() => new("--sdk-version")
    {
        Description = CommandDefinitionStrings.WorkloadInstallVersionOptionDescription,
        HelpName = CommandDefinitionStrings.WorkloadInstallVersionOptionName,
        Hidden = true
    };

    public static Argument<IEnumerable<string>> CreateWorkloadIdArgument() => new("workloadId")
    {
        HelpName = CommandDefinitionStrings.WorkloadIdArgumentName,
        Arity = ArgumentArity.OneOrMore,
        Description = CommandDefinitionStrings.WorkloadIdArgumentDescription
    };

    public static Option<string> CreateConfigOption() => new("--configfile")
    {
        Description = CommandDefinitionStrings.WorkloadInstallConfigFileOptionDescription,
        HelpName = CommandDefinitionStrings.WorkloadInstallConfigFileOptionName
    };

    public static Option<string[]> CreateSourceOption() => new Option<string[]>("--source", "-s")
    {
        Description = CommandDefinitionStrings.WorkloadInstallSourceOptionDescription,
        HelpName = CommandDefinitionStrings.WorkloadInstallSourceOptionName
    }.AllowSingleArgPerToken();

    public static Option<bool> CreateSkipSignCheckOption() => new("--skip-sign-check")
    {
        Description = CommandDefinitionStrings.SkipSignCheckOptionDescription,
        Hidden = true,
        Arity = ArgumentArity.Zero
    };

    public static Option<string> CreateTempDirOption() => new("--temp-dir")
    {
        Description = CommandDefinitionStrings.TempDirOptionDescription
    };

    public const string SkipManifestUpdateOptionName = "--skip-manifest-update";

    public static Option<bool> CreateSkipManifestUpdateOption() => new(SkipManifestUpdateOptionName)
    {
        Description = CommandDefinitionStrings.SkipManifestUpdateOptionDescription,
        Arity = ArgumentArity.Zero
    };
}
