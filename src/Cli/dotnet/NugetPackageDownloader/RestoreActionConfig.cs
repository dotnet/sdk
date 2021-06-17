// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader
{
    /// <param name="Interactive">The flag to enable nuget authentication plugin.
    /// https://docs.microsoft.com/en-us/nuget/reference/extensibility/nuget-cross-platform-authentication-plugin</param>
    internal record RestoreActionConfig(
        bool DisableParallel = false,
        bool NoCache = false,
        bool IgnoreFailedSources = false,
        bool Interactive = false);
}
