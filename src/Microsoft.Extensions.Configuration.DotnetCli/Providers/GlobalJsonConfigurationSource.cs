// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Configuration.Json;

namespace Microsoft.Extensions.Configuration.DotnetCli.Providers;

/// <summary>
/// Configuration source for global.json files.
/// </summary>
public class GlobalJsonConfigurationSource : JsonConfigurationSource
{

    public GlobalJsonConfigurationSource(string workingDirectory)
    {
        Path = System.IO.Path.Combine(workingDirectory, "global.json");
        Optional = true;
        ResolveFileProvider();
    }

    public override IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        EnsureDefaults(builder);
        return new GlobalJsonConfigurationProvider(this);
    }
}
