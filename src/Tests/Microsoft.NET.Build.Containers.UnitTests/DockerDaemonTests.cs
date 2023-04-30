// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace Microsoft.NET.Build.Containers.UnitTests;

[CollectionDefinition("Daemon Tests")]
public class DaemonTestsCollection
{ 
}

[Collection("Daemon Tests")]
public class DockerDaemonTests
{
    [DockerDaemonAvailableFact]
    public async Task Can_detect_when_no_daemon_is_running() {
        // mimic no daemon running by setting the DOCKER_HOST to a nonexistent socket
        try {
            System.Environment.SetEnvironmentVariable("DOCKER_HOST", "tcp://123.123.123.123:12345");
            var available = await new LocalDocker(Console.WriteLine).IsAvailableAsync(default).ConfigureAwait(false);
            Assert.False(available, "No daemon should be listening at that port");
        } finally {
            System.Environment.SetEnvironmentVariable("DOCKER_HOST", null);
        }
    }

    [DockerDaemonAvailableFact]
    public async Task Can_detect_when_daemon_is_running() {
        var available = await new LocalDocker(Console.WriteLine).IsAvailableAsync(default).ConfigureAwait(false);
        Assert.True(available, "Should have found a working daemon");
    }
}
