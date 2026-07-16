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
            var available = await new ContainerRuntime(_loggerFactory, probePlatformNativeCli: false).IsAvailableAsync(default).ConfigureAwait(false);
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
        var available = await new ContainerRuntime(_loggerFactory, probePlatformNativeCli: false).IsAvailableAsync(default).ConfigureAwait(false);
        Assert.IsTrue(available, "Should have found a working daemon");
    }

    [TestMethod]
    [DataRow(true, false, true, true, true, true, false, (int)ContainerRuntimeKind.Wslc)]
    [DataRow(false, true, true, true, true, true, false, (int)ContainerRuntimeKind.MacOSContainer)]
    [DataRow(false, true, false, false, true, true, false, (int)ContainerRuntimeKind.Docker)]
    [DataRow(false, true, false, false, false, true, false, (int)ContainerRuntimeKind.Podman)]
    [DataRow(false, false, true, true, true, false, false, (int)ContainerRuntimeKind.Docker)]
    [DataRow(false, false, false, false, true, true, false, (int)ContainerRuntimeKind.Docker)]
    [DataRow(false, false, false, false, true, true, true, (int)ContainerRuntimeKind.Podman)]
    public void Selects_preferred_available_command(
        bool isWindows,
        bool isMacOS,
        bool wslcAvailable,
        bool macOSContainerAvailable,
        bool dockerAvailable,
        bool podmanAvailable,
        bool isPodmanAlias,
        int expectedRuntime)
    {
        ContainerRuntime runtime = CreateRuntime(
            command => command switch
            {
                ContainerRuntime.WslcCommand => wslcAvailable,
                ContainerRuntime.MacOSContainerCommand => macOSContainerAvailable,
                ContainerRuntime.DockerCommand => dockerAvailable,
                ContainerRuntime.PodmanCommand => podmanAvailable,
                _ => false
            },
            isWindows,
            isMacOS,
            isPodmanAlias);

        Assert.AreEqual((ContainerRuntimeKind)expectedRuntime, runtime.GetTelemetryValue());
    }

    [TestMethod]
    public void Stops_probing_after_platform_native_runtime_succeeds()
    {
        var probedCommands = new List<string>();
        ContainerRuntime runtime = new(
            command: null,
            _loggerFactory,
            (command, _, _) =>
            {
                probedCommands.Add(command);
                return Task.FromResult(true);
            },
            () => false,
            isWindows: true,
            isMacOS: false);

        Assert.AreEqual(ContainerRuntimeKind.Wslc, runtime.GetTelemetryValue());
        Assert.AreSequenceEqual(new[] { ContainerRuntime.WslcCommand }, probedCommands);
    }

    [TestMethod]
    public async Task Times_out_runtime_probes()
    {
        ContainerRuntime runtime = new(
            command: null,
            _loggerFactory,
            async (_, _, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return true;
            },
            () => false,
            isWindows: false,
            isMacOS: false,
            probeTimeout: TimeSpan.FromMilliseconds(10));

        Assert.IsFalse(await runtime.IsAvailableAsync(default));
    }

    [TestMethod]
    public void Does_not_probe_wslc_outside_windows()
    {
        var probedCommands = new System.Collections.Concurrent.ConcurrentBag<string>();
        ContainerRuntime runtime = new(
            command: null,
            _loggerFactory,
            (command, _, _) =>
            {
                probedCommands.Add(command);
                return Task.FromResult(command == ContainerRuntime.WslcCommand);
            },
            () => false,
            isWindows: false,
            isMacOS: false);

        Assert.AreEqual(ContainerRuntimeKind.Unknown, runtime.GetTelemetryValue());
        Assert.DoesNotContain(ContainerRuntime.WslcCommand, probedCommands);
    }

    [TestMethod]
    public void Does_not_probe_container_outside_macos()
    {
        var probedCommands = new System.Collections.Concurrent.ConcurrentBag<string>();
        ContainerRuntime runtime = new(
            command: null,
            _loggerFactory,
            (command, _, _) =>
            {
                probedCommands.Add(command);
                return Task.FromResult(command == ContainerRuntime.MacOSContainerCommand);
            },
            () => false,
            isWindows: false,
            isMacOS: false);

        Assert.AreEqual(ContainerRuntimeKind.Unknown, runtime.GetTelemetryValue());
        Assert.DoesNotContain(ContainerRuntime.MacOSContainerCommand, probedCommands);
    }

    [TestMethod]
    [DataRow(ContainerRuntime.WslcCommand, true, false, "image ls")]
    [DataRow(ContainerRuntime.MacOSContainerCommand, false, true, "system status")]
    public async Task Uses_platform_native_specific_commands(
        string command,
        bool isWindows,
        bool isMacOS,
        string expectedProbeArguments)
    {
        string? probedArguments = null;
        ContainerRuntime runtime = new(
            command,
            _loggerFactory,
            (_, arguments, _) =>
            {
                probedArguments = arguments;
                return Task.FromResult(true);
            },
            () => false,
            isWindows,
            isMacOS);

        Assert.IsTrue(await runtime.IsAvailableAsync(default));
        Assert.AreEqual(expectedProbeArguments, probedArguments);
    }

    [TestMethod]
    [DataRow(KnownLocalRegistryTypes.Wslc, (int)ContainerRuntimeKind.Wslc)]
    [DataRow(KnownLocalRegistryTypes.MacOSContainer, (int)ContainerRuntimeKind.MacOSContainer)]
    public void Creates_explicit_platform_native_registry(string registryType, int expectedRuntime)
    {
        ILocalRegistry registry = KnownLocalRegistryTypes.CreateLocalRegistry(registryType, _loggerFactory);

        ContainerRuntime runtime = (ContainerRuntime)registry;
        Assert.AreEqual((ContainerRuntimeKind)expectedRuntime, runtime.GetTelemetryValue());
        Assert.Contains(registryType, KnownLocalRegistryTypes.SupportedLocalRegistryTypes);
    }

    [TestMethod]
    public void MacOSContainer_registry_forces_oci_image_format()
    {
        ILocalRegistry registry = KnownLocalRegistryTypes.CreateLocalRegistry(KnownLocalRegistryTypes.MacOSContainer, _loggerFactory);
        DestinationImageReference destination = new(registry, "repository", ["tag"]);

        Assert.AreEqual(
            SchemaTypes.OciManifestV1,
            ContainerHelpers.GetManifestMediaType(
                SchemaTypes.DockerManifestV2,
                KnownImageFormats.Docker,
                destination));
    }

    private ContainerRuntime CreateRuntime(Func<string, bool> isAvailable, bool isWindows, bool isMacOS, bool isPodmanAlias)
        => new(
            command: null,
            _loggerFactory,
            (command, _, _) => Task.FromResult(isAvailable(command)),
            () => isPodmanAlias,
            isWindows,
            isMacOS);
}
