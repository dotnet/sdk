// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Provides the values accepted by the <c>LocalRegistry</c> MSBuild property.
/// </summary>
public static class KnownLocalRegistryTypes
{
    /// <summary>Bypasses runtime auto-detection and loads the image through Docker.</summary>
    public const string Docker = nameof(Docker);

    /// <summary>Bypasses runtime auto-detection and loads the image through Podman.</summary>
    public const string Podman = nameof(Podman);

    /// <summary>Bypasses runtime auto-detection and loads the image through WSLC on Windows.</summary>
    public const string Wslc = nameof(Wslc);

    /// <summary>
    /// Gets all values accepted by the <c>LocalRegistry</c> MSBuild property.
    /// </summary>
    public static readonly string[] SupportedLocalRegistryTypes = [Docker, Podman, Wslc];

    internal static ILocalRegistry CreateLocalRegistry(string? type, ILoggerFactory loggerFactory)
    {
        if (string.IsNullOrEmpty(type))
        {
            return new ContainerRuntime(null, loggerFactory);
        }

        return type switch
        {
            Podman => new ContainerRuntime(ContainerRuntime.PodmanCommand, loggerFactory),
            Docker => new ContainerRuntime(ContainerRuntime.DockerCommand, loggerFactory),
            Wslc => new ContainerRuntime(ContainerRuntime.WslcCommand, loggerFactory),
            _ => throw new NotSupportedException(
                Resource.FormatString(
                    nameof(Strings.UnknownLocalRegistryType),
                    type,
                    string.Join(",", SupportedLocalRegistryTypes)))
        };
    }
}
