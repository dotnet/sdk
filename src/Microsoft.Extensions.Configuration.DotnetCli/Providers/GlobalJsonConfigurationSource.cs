// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.Configuration.DotnetCli.Providers;

/// <summary>
/// Configuration source for global.json files.
/// </summary>
public class GlobalJsonConfigurationSource : IConfigurationSource
{
    private readonly string _workingDirectory;

    public GlobalJsonConfigurationSource(string workingDirectory)
    {
        _workingDirectory = workingDirectory ?? throw new ArgumentNullException(nameof(workingDirectory));
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new GlobalJsonConfigurationProvider(_workingDirectory);
    }
}
