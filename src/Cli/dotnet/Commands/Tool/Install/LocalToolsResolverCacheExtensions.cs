// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.Commands.Tool.Install;

internal static class LocalToolsResolverCacheExtensions
{
    public static void SaveToolPackage(
        this ILocalToolsResolverCache localToolsResolverCache,
        IToolPackage toolDownloadedPackage,
        string targetFrameworkToInstall)
    {
        if (localToolsResolverCache == null)
        {
            throw new ArgumentNullException(nameof(localToolsResolverCache));
        }

        if (toolDownloadedPackage == null)
        {
            throw new ArgumentNullException(nameof(toolDownloadedPackage));
        }

        if (string.IsNullOrWhiteSpace(targetFrameworkToInstall))
        {
            throw new ArgumentException("targetFrameworkToInstall cannot be null or whitespace",
                nameof(targetFrameworkToInstall));
        }

        localToolsResolverCache.Save(
            new Dictionary<RestoredCommandIdentifier, ToolCommand>
            {
                [new RestoredCommandIdentifier(
                        toolDownloadedPackage.Id,
                        toolDownloadedPackage.Version,
                        NuGetFramework.Parse(targetFrameworkToInstall),
                        Constants.AnyRid,
                        toolDownloadedPackage.Command.Name)] =
                    toolDownloadedPackage.Command
            });
    }
}
