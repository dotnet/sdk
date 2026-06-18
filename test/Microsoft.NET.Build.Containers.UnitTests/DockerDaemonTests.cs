// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
[DoNotParallelize] // mutates the DOCKER_HOST environment variable
public class DockerDaemonTests : IDisposable
{
    public TestContext TestContext { get; set; } = null!;

    private TestLoggerFactory _loggerFactory = null!;

    [TestInitialize]
    public void Initialize()
    {
        _loggerFactory = new TestLoggerFactory(TestContext);
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
    }

    [TestMethod]
    [DockerAvailableFact(skipPodman: true)] // podman is a local cli not meant for connecting to remote Docker daemons.
    public async Task Can_detect_when_no_daemon_is_running()
    {
        // mimic no daemon running by setting the DOCKER_HOST to a nonexistent socket
        try
        {
            Environment.SetEnvironmentVariable("DOCKER_HOST", "tcp://123.123.123.123:12345");
            var available = await new DockerCli(_loggerFactory).IsAvailableAsync(default).ConfigureAwait(false);
            Assert.IsFalse(available, "No daemon should be listening at that port");
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOCKER_HOST", null);
        }
    }

    [TestMethod]
    [DockerAvailableFact]
    [Ignore("https://github.com/dotnet/sdk/issues/49502")]
    public async Task Can_detect_when_daemon_is_running()
    {
        var available = await new DockerCli(_loggerFactory).IsAvailableAsync(default).ConfigureAwait(false);
        Assert.IsTrue(available, "Should have found a working daemon");
    }
}
