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

    public override bool IsConditionMet => DockerCliStatus.Runtime == ContainerRuntimeKind.Podman;
}

// Cache the Docker/Podman probe for the lifetime of the test process.
internal static class DockerCliStatus
{
    private static readonly Lazy<(bool IsAvailable, ContainerRuntimeKind Runtime)> s_status = new(
        GetStatus,
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static bool IsAvailable => s_status.Value.IsAvailable;
    public static ContainerRuntimeKind Runtime => s_status.Value.Runtime;
    public static string LocalRegistry
        => Runtime switch
        {
            ContainerRuntimeKind.Podman => KnownLocalRegistryTypes.Podman,
            _ => KnownLocalRegistryTypes.Docker
        };

    private static (bool IsAvailable, ContainerRuntimeKind Runtime) GetStatus()
    {
        ContainerRuntime runtime = new(new TestLoggerFactory(), probePlatformNativeCli: false);
        bool isAvailable = runtime.IsAvailable();
        return (isAvailable, runtime.GetTelemetryValue());
    }
}
