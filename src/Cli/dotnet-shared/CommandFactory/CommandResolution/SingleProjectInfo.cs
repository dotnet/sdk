// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.CommandFactory.CommandResolution;

internal class SingleProjectInfo(string name, string version, IEnumerable<ResourceAssemblyInfo> resourceAssemblies)
{
    public string Name { get; } = name;
    public string Version { get; } = version;

    public IEnumerable<ResourceAssemblyInfo> ResourceAssemblies { get; } = resourceAssemblies;

    public string GetOutputName()
    {
        return $"{Name}.dll";
    }
}
