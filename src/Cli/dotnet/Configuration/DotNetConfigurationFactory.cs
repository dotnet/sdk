// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Configuration.DotnetCli.Services;
using Microsoft.Extensions.Configuration.DotnetCli.Models;
using Microsoft.Extensions.Configuration.DotnetCli.Providers;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace Microsoft.DotNet.Cli.Configuration
{
    /// <summary>
    /// Factory for creating and configuring the .NET CLI configuration service.
    /// This is the main entry point for the unified configuration system.
    /// </summary>
    public static class DotNetConfigurationFactory
    {
        /// <summary>
        /// Creates and configures the .NET CLI configuration service with default providers.
        /// This method follows the layered configuration approach: environment variables override global.json,
        /// and configuration is loaded lazily for performance.
        /// </summary>
        /// <param name="workingDirectory">The working directory to search for global.json files. Defaults to current directory.</param>
        /// <returns>A configured IDotNetConfigurationService instance</returns>
        public static IDotNetConfigurationService Create(string? workingDirectory = null)
        {
            workingDirectory ??= Directory.GetCurrentDirectory();

            var configurationBuilder = new ConfigurationBuilder();

            // Configuration sources are added in reverse precedence order
            // Last added has highest precedence

            // 1. global.json (custom provider with key mapping) - lowest precedence
            configurationBuilder.Add(new GlobalJsonConfigurationSource(workingDirectory));

            // 2. Environment variables (custom provider with key mapping) - highest precedence
            configurationBuilder.Add(new DotNetEnvironmentConfigurationSource());

            var configuration = configurationBuilder.Build();

            return new DotNetConfigurationService(configuration);
        }

        /// <summary>
        /// Creates a minimal configuration service with only environment variables.
        /// This is useful for scenarios where global.json lookup is not needed or desirable,
        /// such as in performance-critical paths or testing scenarios.
        /// </summary>
        /// <returns>A minimal IDotNetConfigurationService instance</returns>
        public static IDotNetConfigurationService CreateMinimal()
        {
            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.Add(new DotNetEnvironmentConfigurationSource());
            var configuration = configurationBuilder.Build();
            return new DotNetConfigurationService(configuration);
        }
    }
}
