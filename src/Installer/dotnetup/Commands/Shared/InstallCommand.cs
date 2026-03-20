// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Dotnet.Installation.Internal;

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

    public IDotnetInstallManager DotnetInstaller { get; }
    public ChannelVersionResolver ChannelVersionResolver { get; }

    protected InstallCommand(ParseResult parseResult)
        : base(parseResult)
    {
        InstallPath = parseResult.GetValue(CommonOptions.InstallPathOption);
        ManifestPath = parseResult.GetValue(CommonOptions.ManifestPathOption);
        Interactive = parseResult.GetValue(CommonOptions.InteractiveOption);
        NoProgress = parseResult.GetValue(CommonOptions.NoProgressOption);
        Verbosity = parseResult.GetValue(CommonOptions.VerbosityOption);
        RequireMuxerUpdate = parseResult.GetValue(CommonOptions.RequireMuxerUpdateOption);
        Untracked = parseResult.GetValue(CommonOptions.UntrackedOption);

        DotnetInstaller = new DotnetInstallManager();
        ChannelVersionResolver = new ChannelVersionResolver();
    }
}
