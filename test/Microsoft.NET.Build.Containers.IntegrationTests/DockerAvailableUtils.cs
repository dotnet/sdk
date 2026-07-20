// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

// Availability probes can be expensive and may initialize a local container environment.
// Cache each result for the lifetime of the test process.

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

    public override bool IsConditionMet => DockerCliStatus.Runtime == ContainerRuntimeKind.Podman;
}

public sealed class WslcAvailableCondition : ConditionBaseAttribute
{
    public WslcAvailableCondition()
        : base(ConditionMode.Include)
    {
        IgnoreMessage = "This test requires Windows with an available WSLC container environment.";
    }

    public override string GroupName => nameof(WslcAvailableCondition);

    public override bool IsConditionMet => WslcCliStatus.IsAvailable;
}

public sealed class MacOSContainerAvailableCondition : ConditionBaseAttribute
{
    public MacOSContainerAvailableCondition()
        : base(ConditionMode.Include)
    {
        IgnoreMessage = "This test requires macOS with an available container environment.";
    }

    public override string GroupName => nameof(MacOSContainerAvailableCondition);

    public override bool IsConditionMet => MacOSContainerCliStatus.IsAvailable;
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
           && DockerCliStatus.Runtime != ContainerRuntimeKind.Podman
           && !DockerContainerRuntime.IsContainerdStoreEnabled();
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

internal static class WslcCliStatus
{
    private static readonly Lazy<bool> s_isAvailable = new(
        () => OperatingSystem.IsWindows()
              && new ContainerRuntime(ContainerRuntime.WslcCommand, new Microsoft.NET.TestFramework.TestLoggerFactory()).IsAvailable(),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static bool IsAvailable => s_isAvailable.Value;
}

internal static class MacOSContainerCliStatus
{
    private static readonly Lazy<bool> s_isAvailable = new(
        () => OperatingSystem.IsMacOS()
              && new ContainerRuntime(ContainerRuntime.MacOSContainerCommand, new Microsoft.NET.TestFramework.TestLoggerFactory()).IsAvailable(),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static bool IsAvailable => s_isAvailable.Value;
}

internal static class PodmanCliStatus
{
    private static readonly Lazy<bool> s_isAvailable = new(
        () => new ContainerRuntime(
            ContainerRuntime.PodmanCommand,
            new Microsoft.NET.TestFramework.TestLoggerFactory()).IsAvailable(),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static bool IsAvailable => s_isAvailable.Value;
}

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
        ContainerRuntime runtime = new(new Microsoft.NET.TestFramework.TestLoggerFactory(), probePlatformNativeCli: false);
        bool isAvailable = runtime.IsAvailable();
        return (isAvailable, runtime.GetTelemetryValue());
    }
}

internal static class MultiArchLocalRegistryTestData
{
    public static IEnumerable<string> AvailableRuntimes()
    {
        if (DockerCliStatus.IsAvailable
            && DockerCliStatus.Runtime == ContainerRuntimeKind.Docker
            && DockerContainerRuntime.IsContainerdStoreEnabled())
        {
            yield return KnownLocalRegistryTypes.Docker;
        }

        if (PodmanCliStatus.IsAvailable)
        {
            yield return KnownLocalRegistryTypes.Podman;
        }

        if (WslcCliStatus.IsAvailable)
        {
            yield return KnownLocalRegistryTypes.Wslc;
        }

        if (MacOSContainerCliStatus.IsAvailable)
        {
            yield return KnownLocalRegistryTypes.MacOSContainer;
        }
    }
}
