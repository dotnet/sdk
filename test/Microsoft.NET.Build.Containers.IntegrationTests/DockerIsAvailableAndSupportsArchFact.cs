// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

public class DockerIsAvailableAndSupportsArchFactAttribute : FactAttribute
{
    public DockerIsAvailableAndSupportsArchFactAttribute(
        string arch,
        bool checkContainerdStoreAvailability = false,
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = 0)
        : base(sourceFilePath, sourceLineNumber)
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
