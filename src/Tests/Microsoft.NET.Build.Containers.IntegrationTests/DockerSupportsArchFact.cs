// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.IntegrationTests;

public class DockerSupportsArchFactAttribute : FactAttribute
{
    private readonly string _arch;

    public DockerSupportsArchFactAttribute(string arch)
    {
        _arch = arch;

        if (!DockerSupportsArchHelper.DaemonSupportsArch(_arch))
        {
            base.Skip = $"Skipping test because Docker daemon does not support {_arch}.";
        }
    }
}
