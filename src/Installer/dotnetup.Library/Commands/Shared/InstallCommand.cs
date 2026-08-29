// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

/// <summary>
/// Abstract base for SDK and Runtime install commands.
/// Exposes the shared command-line options so <see cref="InstallWorkflow"/>
/// can access them without taking a dozen parameters.
/// </summary>
internal abstract class InstallCommand : CommandBase
{
    public string? InstallPath { get; }
    public string? ManifestPath { get; }
    public bool Interactive { get; }
    public bool NoProgress { get; }
    public Verbosity Verbosity { get; }
    public bool RequireMuxerUpdate { get; }
    public bool Untracked { get; }
    public IEnvShellProvider? ShellProvider { get; }
    public bool MigrateFromSystem { get; }
    public virtual bool UpdateGlobalJson => false;

    /// <summary>
    /// Whether global.json's <c>sdk.paths</c> may supply the install root for this command.
    /// Install commands honor it so an SDK requested inside a repository lands where that
    /// repository expects it; <c>dotnetup init</c> overrides this to keep the managed hive
    /// independent of the directory the command happens to run in.
    /// </summary>
    public virtual bool UseGlobalJsonSdkPaths => true;
    public virtual IReadOnlyCollection<InstallComponent> MigrationComponents => [];

    public IDotnetEnvironmentManager DotnetEnvironment { get; }
    public ChannelVersionResolver ChannelVersionResolver { get; }

    protected InstallCommand(ParseResult parseResult, string commandName)
        : base(parseResult, commandName)
    {
        InstallPath = parseResult.GetValue(CommonOptions.InstallPathOption);
        ManifestPath = parseResult.GetValue(CommonOptions.ManifestPathOption);
        Interactive = parseResult.GetValue(CommonOptions.InteractiveOption);
        NoProgress = parseResult.GetValue(CommonOptions.NoProgressOption);
        Verbosity = parseResult.GetValue(CommonOptions.VerbosityOption);
        RequireMuxerUpdate = parseResult.GetValue(CommonOptions.RequireMuxerUpdateOption);
        Untracked = parseResult.GetValue(CommonOptions.UntrackedOption);
        ShellProvider = parseResult.GetValue(CommonOptions.ShellOption);
        MigrateFromSystem = parseResult.GetValue(CommonOptions.MigrateFromSystemOption);

        DotnetEnvironment = new DotnetEnvironmentManager();
        ChannelVersionResolver = new ChannelVersionResolver();
    }
}
