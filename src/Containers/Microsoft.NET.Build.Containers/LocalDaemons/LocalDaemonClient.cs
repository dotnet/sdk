// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Docker.DotNet;
using Microsoft.Extensions.Logging;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Creates Docker Engine API clients for the selected Docker-compatible local runtime.
/// </summary>
internal static class LocalDaemonClient
{
    internal static IDockerClient Create(
        string? localRegistry,
        ILoggerFactory loggerFactory,
        out ContainerRuntimeKind runtimeKind)
    {
        DockerClientBuilder builder = new();
        ContainerRuntime runtime = string.IsNullOrEmpty(localRegistry)
            ? new ContainerRuntime(loggerFactory)
            : (ContainerRuntime)KnownLocalRegistryTypes.CreateLocalRegistry(localRegistry, loggerFactory);
        runtimeKind = runtime.GetTelemetryValue();

        if (runtimeKind == ContainerRuntimeKind.Podman && TryGetPodmanEndpoint() is { } podmanEndpoint)
        {
            builder.WithEndpoint(podmanEndpoint);
        }

        return builder.Build();
    }

    internal static Uri? TryGetPodmanEndpoint()
    {
        if (TryGetEndpointFromEnvironment("CONTAINER_HOST") is { } containerHost)
        {
            return containerHost;
        }

        if (TryGetEndpointFromEnvironment("DOCKER_HOST") is { } dockerHost)
        {
            return dockerHost;
        }

        if (!OperatingSystem.IsLinux())
        {
            // Podman Desktop and podman machine forward the API through the platform's
            // default Docker endpoint. DockerClientBuilder resolves that endpoint.
            return null;
        }

        string? runtimeDirectory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (!string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            string rootlessSocket = Path.Combine(runtimeDirectory, "podman", "podman.sock");
            if (File.Exists(rootlessSocket))
            {
                return CreateUnixEndpoint(rootlessSocket);
            }
        }

        const string rootfulSocket = "/run/podman/podman.sock";
        return File.Exists(rootfulSocket) ? CreateUnixEndpoint(rootfulSocket) : null;
    }

    private static Uri? TryGetEndpointFromEnvironment(string variable)
    {
        string? value = Environment.GetEnvironmentVariable(variable);
        return Uri.TryCreate(value, UriKind.Absolute, out Uri? endpoint) && endpoint.Scheme != "ssh"
            ? endpoint
            : null;
    }

    private static Uri CreateUnixEndpoint(string socketPath)
        => new($"unix://{socketPath}");
}
