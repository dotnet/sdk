﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.DotNet.ToolPackage;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.ToolPackage
{
    internal interface IToolPackageDownloader
    {
        IToolPackage InstallPackage(PackageLocation packageLocation, PackageId packageId,
            VerbosityOptions verbosity,
            VersionRange versionRange = null,
            string targetFramework = null,
            bool isGlobalTool = false,
            bool isGlobalToolRollForward = false
        );
    }
}
