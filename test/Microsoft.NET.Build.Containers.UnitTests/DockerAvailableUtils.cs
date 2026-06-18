// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.UnitTests;

/// <summary>
/// MSTest condition attribute that ignores a test when Docker (or Podman) is unavailable on the
/// host, optionally also when running under Podman or without the containerd image store. Apply it
/// alongside <c>[TestMethod]</c> (this is the MSTest counterpart of the xUnit Docker-gated
/// <c>[Fact]</c>/<c>[Theory]</c> attributes).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class DockerAvailableConditionAttribute : ConditionBaseAttribute
{
    public static string LocalRegistry => DockerCliStatus.LocalRegistry;

    public DockerAvailableConditionAttribute(bool skipPodman = false, bool checkContainerdStoreAvailability = false)
        : base(ConditionMode.Include)
    {
        IgnoreMessage = DockerCliStatus.GetSkipReason(skipPodman, checkContainerdStoreAvailability);
    }

    public override bool IsConditionMet => IgnoreMessage is null;

    public override string GroupName => nameof(DockerAvailableConditionAttribute);
}

// tiny optimization - since there are many instances of this attribute we should only get
// the daemon status once
static file class DockerCliStatus
{
    public static readonly bool IsAvailable;
    public static readonly string? Command;
    public static string LocalRegistry
        => Command == DockerCli.PodmanCommand ? KnownLocalRegistryTypes.Podman
                                              : KnownLocalRegistryTypes.Docker;

    static DockerCliStatus()
    {
        DockerCli cli = new(new TestLoggerFactory());
        IsAvailable = cli.IsAvailable();
        Command = cli.GetCommand();
    }

    /// <summary>
    /// Returns the reason the test should be ignored, or <see langword="null"/> if the test should run.
    /// </summary>
    public static string? GetSkipReason(bool skipPodman, bool checkContainerdStoreAvailability)
    {
        if (!IsAvailable)
        {
            return "Skipping test because Docker is not available on this host.";
        }

        if (checkContainerdStoreAvailability && Command != DockerCli.PodmanCommand && !DockerCli.IsContainerdStoreEnabledForDocker())
        {
            return "Skipping test because Docker daemon is not using containerd as the storage driver.";
        }

        if (skipPodman && Command == DockerCli.PodmanCommand)
        {
            return $"Skipping test with {Command} cli.";
        }

        return null;
    }
}
