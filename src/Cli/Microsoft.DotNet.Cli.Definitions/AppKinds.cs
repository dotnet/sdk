// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands;

[Flags]
internal enum AppKinds
{
    None = 0,
    ProjectBased = 1 << 0,
    FileBased = 1 << 1,
    Any = ProjectBased | FileBased,
}
