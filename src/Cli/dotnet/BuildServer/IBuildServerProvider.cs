// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Cli.BuildServer;

[Flags]
internal enum ServerEnumerationFlags
{
    None = 0,
    MSBuild = 1 << 0,
    VBCSCompiler = 1 << 1,
    Razor = 1 << 2,
    Unified = 1 << 3,
    All = MSBuild | VBCSCompiler | Razor | Unified
}

internal interface IBuildServerProvider
{
    IEnumerable<IBuildServer> EnumerateBuildServers(ServerEnumerationFlags flags = ServerEnumerationFlags.All);
}
