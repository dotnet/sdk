// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers.UnitTests;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

/// <summary>
/// MSTest condition attribute that extends <see cref="DockerAvailableConditionAttribute"/> with an
/// additional check that the Docker daemon supports the requested <paramref name="arch"/>. Apply it
/// alongside <c>[TestMethod]</c> (this is the MSTest counterpart of the xUnit Docker/arch-gated
/// <c>[Fact]</c>/<c>[Theory]</c> attributes).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class DockerIsAvailableAndSupportsArchConditionAttribute : DockerAvailableConditionAttribute
{
    public DockerIsAvailableAndSupportsArchConditionAttribute(string arch, bool checkContainerdStoreAvailability = false)
        : base(checkContainerdStoreAvailability: checkContainerdStoreAvailability)
    {
        // The base ctor already populated IgnoreMessage when Docker is unavailable or the containerd
        // store is required but missing; only run the (more expensive) arch probe if we got that far.
        if (IgnoreMessage is null && !DockerSupportsArchHelper.DaemonSupportsArch(arch))
        {
            IgnoreMessage = $"Skipping test because Docker daemon does not support {arch}.";
        }
    }

    public override string GroupName => nameof(DockerIsAvailableAndSupportsArchConditionAttribute);
}
