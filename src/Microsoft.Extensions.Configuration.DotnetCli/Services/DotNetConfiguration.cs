// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Configuration.DotnetCli.Providers;

namespace Microsoft.Extensions.Configuration.DotnetCli.Services;

/// <summary>
/// Factory for creating .NET CLI configuration instances.
/// </summary>
public static class DotNetConfiguration
{
    /// <summary>
    /// Creates a complete configuration instance with all providers.
    /// </summary>
    /// <param name="workingDirectory">The working directory to search for configuration files. Defaults to current directory.</param>
    /// <returns>A configured IConfiguration instance.</returns>
    public static IConfiguration Create(string? workingDirectory = null)
    {
        var builder = new ConfigurationBuilder();

        // Priority order (last wins):
        // 1. dotnet.config (if it exists) - future enhancement
        // 2. global.json (custom provider with key mapping)
        // 3. Environment variables with DOTNET_ prefix (with key mapping)
        // 4. Command line arguments (handled separately)

        workingDirectory ??= Directory.GetCurrentDirectory();

        // Add dotnet.config with custom key mapping
        builder.Add(new DotNetConfigurationSource(workingDirectory));

        // Add global.json with a custom configuration provider that maps keys
        builder.Add(new GlobalJsonConfigurationSource(workingDirectory));

        // Add DOTNET_ prefixed environment variables with key mapping
        builder.Add(new DotNetEnvironmentConfigurationSource());

        return builder.Build();
    }

    /// <summary>
    /// Creates a strongly-typed configuration service with all providers.
    /// </summary>
    /// <param name="workingDirectory">The working directory to search for configuration files. Defaults to current directory.</param>
    /// <returns>A strongly-typed configuration service.</returns>
    public static IDotNetConfigurationService CreateTyped(string? workingDirectory = null)
    {
        var configuration = Create(workingDirectory);
        return new DotNetConfigurationService(configuration);
    }

    /// <summary>
    /// Creates a minimal configuration service that only loads environment variables.
    /// This is the fastest option for scenarios that don't need file-based configuration.
    /// </summary>
    /// <param name="workingDirectory">The working directory (unused for minimal configuration).</param>
    /// <returns>A minimal strongly-typed configuration service.</returns>
    public static IDotNetConfigurationService CreateMinimal(string? workingDirectory = null)
    {
        var builder = new ConfigurationBuilder();

        // Only add environment variables for minimal overhead
        builder.Add(new DotNetEnvironmentConfigurationSource());

        var configuration = builder.Build();
        return new DotNetConfigurationService(configuration);
    }
}
