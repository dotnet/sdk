// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

// MSTest condition attributes for Docker availability gating, mirroring the companion
// Microsoft.NET.Build.Containers.UnitTests project. Each invocation queries the docker CLI
// on first use and caches the result in DockerCliStatus (file-scoped class) so it does not
// pollute the rest of the namespace.

public sealed class DockerUnavailableCondition : ConditionBaseAttribute
{
    public static string LocalRegistry => DockerCliStatus.LocalRegistry;

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

public sealed class ContainerdStoreUnavailableCondition : ConditionBaseAttribute
{
    public ContainerdStoreUnavailableCondition()
        : base(ConditionMode.Exclude)
    {
        IgnoreMessage = "Skipping test because Docker daemon is not using containerd as the storage driver.";
    }

    public override string GroupName => nameof(ContainerdStoreUnavailableCondition);

    public override bool IsConditionMet
        => DockerCliStatus.IsAvailable
           && DockerCliStatus.Command != DockerCli.PodmanCommand
           && !DockerCli.IsContainerdStoreEnabledForDocker();
}

public sealed class DockerSupportsArchCondition : ConditionBaseAttribute
{
    private readonly string _arch;
    private readonly bool _checkContainerdStoreAvailability;

    public DockerSupportsArchCondition(string arch, bool checkContainerdStoreAvailability = false)
        : base(ConditionMode.Include)
    {
        _arch = arch;
        _checkContainerdStoreAvailability = checkContainerdStoreAvailability;
        IgnoreMessage = $"Skipping test because Docker is not available or does not support {arch}.";
    }

    public override string GroupName => nameof(DockerSupportsArchCondition);

    public override bool IsConditionMet
        => DockerSupportsArchHelper.DaemonIsAvailable
           && (!_checkContainerdStoreAvailability || ContainerCli.IsPodman || DockerSupportsArchHelper.IsContainerdStoreEnabledForDocker)
           && DockerSupportsArchHelper.DaemonSupportsArch(_arch);
}

// tiny optimization - since there are many instances of these attributes we should only
// query the daemon status once.
static file class DockerCliStatus
{
    public static readonly bool IsAvailable;
    public static readonly string? Command;
    public static string LocalRegistry
        => Command == DockerCli.PodmanCommand ? KnownLocalRegistryTypes.Podman
                                              : KnownLocalRegistryTypes.Docker;

    static DockerCliStatus()
    {
        DockerCli cli = new(new Microsoft.NET.TestFramework.TestLoggerFactory());
        IsAvailable = cli.IsAvailable();
        Command = cli.GetCommand();
    }
}
