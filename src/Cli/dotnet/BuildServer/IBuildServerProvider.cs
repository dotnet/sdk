// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Cli.BuildServer;

[Flags]
internal enum ServerEnumerationFlags
{
    None = 0,
    MSBuild = 1,
    VBCSCompiler = 2,
    Razor = 4,
    Unified = 5,
    All = MSBuild | VBCSCompiler | Razor | Unified
}

internal interface IBuildServerProvider
{
    IEnumerable<IBuildServer> EnumerateBuildServers(ServerEnumerationFlags flags = ServerEnumerationFlags.All);
}
