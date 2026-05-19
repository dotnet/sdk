// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.ToolManifest;

internal interface IToolManifestEditor
{
    void Add(FilePath manifest, PackageId packageId, NuGetVersion nuGetVersion, ToolCommandName[] toolCommandNames, bool rollForward = false);
    void Remove(FilePath manifest, PackageId packageId);
    void Edit(FilePath manifest, PackageId packageId, NuGetVersion newNuGetVersion, ToolCommandName[] newToolCommandNames);
}
