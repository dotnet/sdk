// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.ToolManifest
{
    internal interface IToolManifestEditor
    {
        void Add(FilePath manifest, PackageId packageId, NuGetVersion nuGetVersion, ToolCommandName[] toolCommandNames);
        void Remove(FilePath manifest, PackageId packageId);
        void Edit(FilePath manifest, PackageId packageId, NuGetVersion newNuGetVersion, ToolCommandName[] newToolCommandNames);
    }
}
