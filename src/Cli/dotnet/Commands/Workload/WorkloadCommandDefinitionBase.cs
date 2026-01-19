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
        Description = CliCommandStrings.WorkloadInstallVersionOptionDescription,
        HelpName = CliCommandStrings.WorkloadInstallVersionOptionName,
        Hidden = true
    };

    public static Argument<IEnumerable<string>> CreateWorkloadIdArgument() => new("workloadId")
    {
        HelpName = CliCommandStrings.WorkloadIdArgumentName,
        Arity = ArgumentArity.OneOrMore,
        Description = CliCommandStrings.WorkloadIdArgumentDescription
    };

    public static Option<string> CreateConfigOption() => new("--configfile")
    {
        Description = CliCommandStrings.WorkloadInstallConfigFileOptionDescription,
        HelpName = CliCommandStrings.WorkloadInstallConfigFileOptionName
    };

    public static Option<string[]> CreateSourceOption() => new Option<string[]>("--source", "-s")
    {
        Description = CliCommandStrings.WorkloadInstallSourceOptionDescription,
        HelpName = CliCommandStrings.WorkloadInstallSourceOptionName
    }.AllowSingleArgPerToken();

    public static Option<bool> CreateSkipSignCheckOption() => new("--skip-sign-check")
    {
        Description = CliCommandStrings.SkipSignCheckOptionDescription,
        Hidden = true,
        Arity = ArgumentArity.Zero
    };

    public static Option<string> CreateTempDirOption() => new("--temp-dir")
    {
        Description = CliCommandStrings.TempDirOptionDescription
    };

    public const string SkipManifestUpdateOptionName = "--skip-manifest-update";

    public static Option<bool> CreateSkipManifestUpdateOption() => new(SkipManifestUpdateOptionName)
    {
        Description = CliCommandStrings.SkipManifestUpdateOptionDescription,
        Arity = ArgumentArity.Zero
    };
}
