// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

// xUnit-flavoured Docker availability attributes for the IntegrationTests project.
//
// The companion Microsoft.NET.Build.Containers.UnitTests project was migrated to
// MSTest.Sdk and now exposes a `DockerUnavailableCondition : ConditionBaseAttribute`
// instead of the previous `DockerAvailableFactAttribute : FactAttribute` /
// `DockerAvailableTheoryAttribute : TheoryAttribute` pair. IntegrationTests is still
// an xUnit project, so it needs its own xUnit-compatible attributes.
//
// Implementation mirrors the previous UnitTests version: each invocation shells out to
// the docker CLI on first use and caches the result. Caching lives in DockerCliStatus
// (file-scoped class) so it does not pollute the rest of the namespace.

public class DockerAvailableTheoryAttribute : TheoryAttribute
{
    public static string LocalRegistry => DockerCliStatus.LocalRegistry;

    public DockerAvailableTheoryAttribute(
        bool skipPodman = false,
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = 0)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!DockerCliStatus.IsAvailable)
        {
            base.Skip = "Skipping test because Docker is not available on this host.";
        }

        if (skipPodman && DockerCliStatus.Command == DockerCli.PodmanCommand)
        {
            base.Skip = $"Skipping test with {DockerCliStatus.Command} cli.";
        }
    }
}

public class DockerAvailableFactAttribute : FactAttribute
{
    public static string LocalRegistry => DockerCliStatus.LocalRegistry;

    public DockerAvailableFactAttribute(
        bool skipPodman = false,
        bool checkContainerdStoreAvailability = false,
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = 0)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!DockerCliStatus.IsAvailable)
        {
            base.Skip = "Skipping test because Docker is not available on this host.";
        }
        else if (checkContainerdStoreAvailability && DockerCliStatus.Command != DockerCli.PodmanCommand && !DockerCli.IsContainerdStoreEnabledForDocker())
        {
            base.Skip = "Skipping test because Docker daemon is not using containerd as the storage driver.";
        }
        else if (skipPodman && DockerCliStatus.Command == DockerCli.PodmanCommand)
        {
            base.Skip = $"Skipping test with {DockerCliStatus.Command} cli.";
        }
    }
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
