// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload;

/// <summary>
/// Shared options of workload install and update commands.
/// </summary>
internal abstract class InstallingWorkloadCommandDefinition : WorkloadCommandDefinitionBase
{
    public sealed override Option<string> TempDirOption { get; } = CreateTempDirOption();
    public sealed override Option<Utils.VerbosityOptions> VerbosityOption { get; } = CommonOptions.CreateVerbosityOption(Utils.VerbosityOptions.normal);

    public readonly Option<IEnumerable<string>> WorkloadSetVersionOption = new("--version")
    {
        Description = CommandDefinitionStrings.WorkloadSetVersionOptionDescription,
        AllowMultipleArgumentsPerToken = true
    };

    public readonly Option<bool> PrintDownloadLinkOnlyOption = new("--print-download-link-only")
    {
        Description = CommandDefinitionStrings.PrintDownloadLinkOnlyDescription,
        Hidden = true
    };

    public readonly Option<string> FromCacheOption = new("--from-cache")
    {
        Description = CommandDefinitionStrings.FromCacheOptionDescription,
        HelpName = CommandDefinitionStrings.FromCacheOptionArgumentName,
        Hidden = true
    };

    public readonly Option<bool> IncludePreviewOption = new("--include-previews")
    {
        Description = CommandDefinitionStrings.IncludePreviewOptionDescription
    };

    public readonly Option<string> DownloadToCacheOption = new("--download-to-cache")
    {
        Description = CommandDefinitionStrings.DownloadToCacheOptionDescription,
        HelpName = CommandDefinitionStrings.DownloadToCacheOptionArgumentName,
        Hidden = true
    };

    public readonly Option<string> FromRollbackFileOption = new("--from-rollback-file")
    {
        Description = CommandDefinitionStrings.FromRollbackDefinitionOptionDescription,
        Hidden = true
    };

    public readonly Option<string> ConfigOption = CreateConfigOption();

    public readonly Option<string[]> SourceOption = CreateSourceOption();

    public readonly Option<string> SdkVersionOption = CreateSdkVersionOption();

    public sealed override Option<bool> SkipSignCheckOption { get; } = CreateSkipSignCheckOption();

    public sealed override NuGetRestoreOptions RestoreOptions { get; } = new();

    public InstallingWorkloadCommandDefinition(string name, string description)
        : base(name, description)
    {
        Options.Add(TempDirOption);
        Options.Add(VerbosityOption);
        Options.Add(SdkVersionOption);
        Options.Add(ConfigOption);
        Options.Add(SourceOption);
        Options.Add(PrintDownloadLinkOnlyOption);
        Options.Add(FromCacheOption);
        Options.Add(DownloadToCacheOption);
        Options.Add(IncludePreviewOption);
        Options.Add(FromRollbackFileOption);
        Options.Add(SkipSignCheckOption);
        Options.Add(WorkloadSetVersionOption);

        RestoreOptions.AddTo(Options);
    }
}
