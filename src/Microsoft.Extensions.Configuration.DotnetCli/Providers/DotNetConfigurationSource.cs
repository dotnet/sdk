// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Configuration.Ini;

namespace Microsoft.Extensions.Configuration.DotnetCli.Providers;

/// <summary>
/// Configuration source for dotnet.config INI files with key mapping.
/// </summary>
public class DotNetConfigurationSource : IniConfigurationSource
{
    public DotNetConfigurationSource(string workingDirectory)
    {
        Path = FindDotNetConfigPath(workingDirectory);
        Optional = true; // Make it optional since dotnet.config may not exist
    }
    /// <summary>
    /// Builds the configuration provider for dotnet.config files.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <returns>The configuration provider.</returns>
    public override IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        EnsureDefaults(builder);
        return new DotNetConfigurationProvider(this);
    }

    private static string FindDotNetConfigPath(string? workingDirectory = null)
    {
        string? directory = workingDirectory ?? Directory.GetCurrentDirectory();
        // Search for dotnet.config in the current directory and upwards
        while (directory != null)
        {
            string dotnetConfigPath = System.IO.Path.Combine(directory, "dotnet.config");
            if (File.Exists(dotnetConfigPath))
            {
                return dotnetConfigPath;
            }

            directory = System.IO.Path.GetDirectoryName(directory);
        }
        return "dotnet.config"; // Return default path even if not found
    }
}
