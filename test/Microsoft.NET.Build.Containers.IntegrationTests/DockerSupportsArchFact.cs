// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.IntegrationTests;

public class DockerIsAvailableAndSupportsArchFactAttribute : FactAttribute
{
    public DockerIsAvailableAndSupportsArchFactAttribute(string arch)
    {
        if (!DockerSupportsArchHelper.DaemonIsAvailable)
        {
            base.Skip = "Skipping test because Docker is not available on this host.";
        }
        else if (!DockerSupportsArchHelper.DaemonSupportsArch(arch))
        {
            base.Skip = $"Skipping test because Docker daemon does not support {arch}.";
        }
    }
}
