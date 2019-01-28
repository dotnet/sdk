// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;
using NuGet.RuntimeModel;

namespace Microsoft.NET.Build.Tasks
{
    internal static class ApphostResolver
    {
        public static (ITaskItem[] AppHost, List<TaskItem> AdditionalPackagesToDownload) GetAppHostItem(
            string appHostPackPattern,
            string appHostKnownRuntimeIdentifiers,
            string appHostPackVersion,
            string appHostRuntimeIdentifier,
            string outputItemName, string targetingPackRoot, RuntimeGraph getRuntimeGraph,
            string dotNetAppHostExecutableNameWithoutExtension, Logger logger)
        {
            var additionalPackagesToDownload = new List<TaskItem>();
            if (!String.IsNullOrEmpty(appHostRuntimeIdentifier) && !String.IsNullOrEmpty(appHostPackPattern))
            {
                //  Choose AppHost RID as best match of the specified RID
                string bestAppHostRuntimeIdentifier = getRuntimeGraph.GetBestRuntimeIdentifier(appHostRuntimeIdentifier, appHostKnownRuntimeIdentifiers, out bool wasInGraph);

                if (bestAppHostRuntimeIdentifier == null)
                {
                    if (wasInGraph)
                    {
                        //  NETSDK1084: There was no app host for available for the specified RuntimeIdentifier '{0}'.
                        logger.LogError(Strings.NoAppHostAvailable, appHostRuntimeIdentifier);
                    }
                    else
                    {
                        //  NETSDK1083: The specified RuntimeIdentifier '{0}' is not recognized.
                        logger.LogError(Strings.UnsupportedRuntimeIdentifier, appHostRuntimeIdentifier);
                    }
                }
                else
                {
                    string appHostPackName = appHostPackPattern.Replace("**RID**", bestAppHostRuntimeIdentifier);

                    string appHostRelativePathInPackage = Path.Combine("runtimes", bestAppHostRuntimeIdentifier, "native",
                        dotNetAppHostExecutableNameWithoutExtension +
                        ExecutableExtension.ForRuntimeIdentifier(bestAppHostRuntimeIdentifier));


                    TaskItem appHostItem = new TaskItem(outputItemName);
                    string appHostPackPath = null;
                    if (!String.IsNullOrEmpty(targetingPackRoot))
                    {
                        appHostPackPath = Path.Combine(targetingPackRoot, appHostPackName, appHostPackVersion);
                    }

                    if (appHostPackPath != null && Directory.Exists(appHostPackPath))
                    {
                        //  Use AppHost from packs folder
                        appHostItem.SetMetadata(MetadataKeys.Path, Path.Combine(appHostPackPath, appHostRelativePathInPackage));
                    }
                    else
                    {
                        //  Download apphost pack
                        TaskItem packageToDownload = new TaskItem(appHostPackName);
                        packageToDownload.SetMetadata(MetadataKeys.Version, appHostPackVersion);
                        additionalPackagesToDownload.Add(packageToDownload);

                        appHostItem.SetMetadata(MetadataKeys.RuntimeIdentifier, appHostRuntimeIdentifier);
                        appHostItem.SetMetadata(MetadataKeys.PackageName, appHostPackName);
                        appHostItem.SetMetadata(MetadataKeys.PackageVersion, appHostPackVersion);
                        appHostItem.SetMetadata(MetadataKeys.RelativePath, appHostRelativePathInPackage);
                    }

                    return (new ITaskItem[] {appHostItem}, additionalPackagesToDownload);
                }
            }

            return (null, additionalPackagesToDownload);
        }
    }
}
