// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Configuration.DotnetCli.Providers;
using System.Collections.Concurrent;

namespace Microsoft.Extensions.Configuration.DotnetCli;

/// <summary>
/// Factory for creating and configuring the .NET CLI configuration service.
/// This is the main entry point for the unified configuration system.
/// </summary>
public static class DotNetConfigurationFactory
{
    // Cache for the default configuration service instance
    private static readonly Lazy<DotNetCliConfiguration> _defaultInstance = new(() => CreateInternal(null), LazyThreadSafetyMode.ExecutionAndPublication);

    // Cache for configuration services by working directory
    private static readonly ConcurrentDictionary<string, Lazy<DotNetCliConfiguration>> _instancesByDirectory = new();

    /// <summary>
    /// Creates and configures the .NET CLI configuration service with default providers.
    /// This method follows the layered configuration approach: environment variables override global.json,
    /// and configuration is loaded lazily for performance.
    /// Results are cached to avoid repeated expensive configuration building.
    /// </summary>
    /// <param name="workingDirectory">The working directory to search for global.json files. Defaults to current directory.</param>
    /// <returns>A configured DotNetCliConfiguration instance</returns>
    public static DotNetCliConfiguration Create(string? workingDirectory = null)
    {
        if (workingDirectory == null)
        {
            // Use the default cached instance for null working directory
            return _defaultInstance.Value;
        }

        // Normalize the working directory path for consistent caching
        var normalizedPath = Path.GetFullPath(workingDirectory);

        // Get or create a cached instance for this specific working directory
        var lazyInstance = _instancesByDirectory.GetOrAdd(normalizedPath,
            path => new Lazy<DotNetCliConfiguration>(() => CreateInternal(path), LazyThreadSafetyMode.ExecutionAndPublication));

        return lazyInstance.Value;
    }

    /// <summary>
    /// Internal method that performs the actual configuration creation without caching.
    /// </summary>
    /// <param name="workingDirectory">The working directory to search for configuration files.</param>
    /// <returns>A configured DotNetConfigurationService instance</returns>
    private static DotNetCliConfiguration CreateInternal(string? workingDirectory)
    {
        workingDirectory ??= Directory.GetCurrentDirectory();

        var configurationBuilder = new ConfigurationBuilder();

        // Configuration sources are added in reverse precedence order
        // Last added has highest precedence

        // 1. dotnet.config (custom provider with key mapping) - lowest precedence
        configurationBuilder.Add(new DotNetConfigurationSource(workingDirectory));

        // 2. global.json (custom provider with key mapping) - medium precedence
        configurationBuilder.Add(new GlobalJsonConfigurationSource(workingDirectory));

        // 3. Environment variables (custom provider with key mapping) - highest precedence
        configurationBuilder.Add(new DotNetEnvironmentConfigurationSource());

        var configuration = configurationBuilder.Build();

        return new DotNetCliConfiguration(configuration);
    }

    /// <summary>
    /// Creates a minimal configuration service with only environment variables.
    /// This is useful for scenarios where global.json lookup is not needed or desirable,
    /// such as in performance-critical paths or testing scenarios.
    /// </summary>
    /// <returns>A minimal DotNetConfigurationService instance</returns>
    public static DotNetCliConfiguration CreateMinimal()
    {
        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.Add(new DotNetEnvironmentConfigurationSource());
        var configuration = configurationBuilder.Build();
        return new DotNetCliConfiguration(configuration);
    }
}
