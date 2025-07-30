// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.Configuration.DotnetCli.Providers;

/// <summary>
/// Configuration source for DOTNET_ prefixed environment variables.
/// </summary>
public class DotNetEnvironmentConfigurationSource : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new DotNetEnvironmentConfigurationProvider();
    }
}
