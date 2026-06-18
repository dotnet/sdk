// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.IntegrationTests;

/// <summary>
/// MSTest condition attribute that ignores a test when Docker is unavailable on the host, when the
/// requested <paramref name="arch"/> is not supported by the daemon, or (optionally) when the
/// containerd image store is not enabled. This is the MSTest counterpart of the xUnit
/// <c>DockerIsAvailableAndSupportsArchFactAttribute</c>; apply it alongside <c>[TestMethod]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class DockerIsAvailableAndSupportsArchFactAttribute : ConditionBaseAttribute
{
    public DockerIsAvailableAndSupportsArchFactAttribute(string arch, bool checkContainerdStoreAvailability = false)
        : base(ConditionMode.Include)
    {
        if (!DockerSupportsArchHelper.DaemonIsAvailable)
        {
            IgnoreMessage = "Skipping test because Docker is not available on this host.";
        }
        else if (checkContainerdStoreAvailability && !ContainerCli.IsPodman && !DockerSupportsArchHelper.IsContainerdStoreEnabledForDocker)
        {
            IgnoreMessage = "Skipping test because Docker daemon is not using containerd as the storage driver.";
        }
        else if (!DockerSupportsArchHelper.DaemonSupportsArch(arch))
        {
            IgnoreMessage = $"Skipping test because Docker daemon does not support {arch}.";
        }
    }

    public override bool IsConditionMet => IgnoreMessage is null;

    public override string GroupName => nameof(DockerIsAvailableAndSupportsArchFactAttribute);
}
