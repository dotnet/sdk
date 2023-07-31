// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.UnitTests;

public class DockerAvailableTheoryAttribute : TheoryAttribute
{
    public DockerAvailableTheoryAttribute(bool skipPodman = false)
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
    public DockerAvailableFactAttribute(bool skipPodman = false)
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

// tiny optimization - since there are many instances of this attribute we should only get
// the daemon status once
file static class DockerCliStatus
{
    public static readonly bool IsAvailable;
    public static readonly string? Command;

    static DockerCliStatus()
    {
        DockerCli cli = new(new TestLoggerFactory());
        IsAvailable = cli.IsAvailable();
        Command = cli.GetCommand();
    }
}
