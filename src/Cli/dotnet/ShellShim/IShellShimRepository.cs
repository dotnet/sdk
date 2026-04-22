// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli.ShellShim;

internal interface IShellShimRepository
{
    void CreateShim(ToolCommand toolCommand, IReadOnlyList<FilePath> packagedShims = null);

    void RemoveShim(ToolCommand toolCommand);
}
