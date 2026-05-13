// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.IntegrationTests;

public class DockerIsAvailableAndSupportsArchTheoryAttribute : TheoryAttribute
{
    public DockerIsAvailableAndSupportsArchTheoryAttribute(string arch, bool checkContainerdStoreAvailability = false)
    {
        if (!DockerSupportsArchHelper.DaemonIsAvailable)
        {
            base.Skip = "Skipping test because Docker is not available on this host.";
        }
        else if (checkContainerdStoreAvailability && !DockerSupportsArchHelper.IsContainerdStoreEnabledForDocker)
        {
            base.Skip = "Skipping test because Docker daemon is not using containerd as the storage driver.";
        }
        else if (!DockerSupportsArchHelper.DaemonSupportsArch(arch))
        {
            base.Skip = $"Skipping test because Docker daemon does not support {arch}.";
        }
    }
}