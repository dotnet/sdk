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
        Path = System.IO.Path.Combine(workingDirectory, "dotnet.config");
        Optional = true; // Make it optional since dotnet.config may not exist
        ResolveFileProvider();
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
}
