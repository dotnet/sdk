// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.UnitTests;

public sealed class DockerUnavailableCondition : ConditionBaseAttribute
{
    public DockerUnavailableCondition()
        : base(ConditionMode.Exclude)
    {
        IgnoreMessage = "Skipping test because Docker is not available on this host.";
    }

    public override string GroupName => nameof(DockerUnavailableCondition);

    public override bool IsConditionMet => !DockerCliStatus.IsAvailable;
}

public sealed class PodmanCliCondition : ConditionBaseAttribute
{
    public PodmanCliCondition()
        : base(ConditionMode.Exclude)
    {
        IgnoreMessage = "Skipping test with podman cli.";
    }

    public override string GroupName => nameof(PodmanCliCondition);

    public override bool IsConditionMet => DockerCliStatus.Command == DockerCli.PodmanCommand;
}

// tiny optimization - since there are many instances of this attribute we should only get
// the daemon status once
static file class DockerCliStatus
{
    public static readonly bool IsAvailable;
    public static readonly string? Command;
    public static string LocalRegistry
        => Command == DockerCli.PodmanCommand ? KnownLocalRegistryTypes.Podman
                                              : KnownLocalRegistryTypes.Docker;

    static DockerCliStatus()
    {
        DockerCli cli = new(new TestLoggerFactory());
        IsAvailable = cli.IsAvailable();
        Command = cli.GetCommand();
    }
}
