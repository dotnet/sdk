// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
// Mutates the process-global DOCKER_HOST environment variable, so it must not run
// concurrently with other tests under method-level parallelization.
[DoNotParallelize]
public class DockerDaemonTests : IDisposable
{
    private readonly TestLoggerFactory _loggerFactory;

    public TestContext TestContext { get; }

    public DockerDaemonTests(TestContext testContext)
    {
        TestContext = testContext;
        _loggerFactory = new TestLoggerFactory(testContext);
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
    }

    [TestMethod]
    [DockerUnavailableCondition]
    [PodmanCliCondition] // podman is a local cli not meant for connecting to remote Docker daemons.
    public async Task Can_detect_when_no_daemon_is_running()
    {
        // mimic no daemon running by setting the DOCKER_HOST to a nonexistent socket
        string? originalDockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");
        try
        {
            Environment.SetEnvironmentVariable("DOCKER_HOST", "tcp://123.123.123.123:12345");
            var available = await new ContainerRuntime(_loggerFactory).IsAvailableAsync(default).ConfigureAwait(false);
            Assert.IsFalse(available, "No daemon should be listening at that port");
        }
        finally
        {
            // restore the original DOCKER_HOST rather than clearing it, so a value set on
            // the test host (CI or developer machine) doesn't leak into subsequent tests
            Environment.SetEnvironmentVariable("DOCKER_HOST", originalDockerHost);
        }
    }

    [TestMethod]
    [Ignore("https://github.com/dotnet/sdk/issues/49502")]
    public async Task Can_detect_when_daemon_is_running()
    {
        var available = await new ContainerRuntime(_loggerFactory).IsAvailableAsync(default).ConfigureAwait(false);
        Assert.IsTrue(available, "Should have found a working daemon");
    }

    [TestMethod]
    [DataRow(true, false, false, (int)ContainerRuntimeKind.Docker)]
    [DataRow(false, true, false, (int)ContainerRuntimeKind.Podman)]
    [DataRow(true, true, false, (int)ContainerRuntimeKind.Docker)]
    [DataRow(true, true, true, (int)ContainerRuntimeKind.Podman)]
    public void Selects_preferred_available_command(
        bool dockerAvailable,
        bool podmanAvailable,
        bool isPodmanAlias,
        int expectedRuntime)
    {
        ContainerRuntime runtime = CreateRuntime(
            command => command switch
            {
                ContainerRuntime.DockerCommand => dockerAvailable,
                ContainerRuntime.PodmanCommand => podmanAvailable,
                _ => false
            },
            isPodmanAlias);

        Assert.AreEqual((ContainerRuntimeKind)expectedRuntime, runtime.GetTelemetryValue());
    }

    private ContainerRuntime CreateRuntime(Func<string, bool> isAvailable, bool isPodmanAlias)
        => new(
            command: null,
            _loggerFactory,
            (command, _, _) => Task.FromResult(isAvailable(command)),
            () => isPodmanAlias);
}
