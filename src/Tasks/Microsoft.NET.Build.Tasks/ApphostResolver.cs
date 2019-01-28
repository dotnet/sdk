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
    internal class ApphostResolver
    {
        public ApphostResolver(string appHostPackPattern,
                               string appHostKnownRuntimeIdentifiers,
                               string appHostPackVersion,
                               string targetingPackRoot,
                               RuntimeGraph runtimeGraph,
                               string dotNetAppHostExecutableNameWithoutExtension,
                               Logger logger)
        {
            AppHostPackPattern = appHostPackPattern;
            AppHostKnownRuntimeIdentifiers = appHostKnownRuntimeIdentifiers;
            AppHostPackVersion = appHostPackVersion;
            TargetingPackRoot = targetingPackRoot;
            RuntimeGraph = runtimeGraph;
            DotNetAppHostExecutableNameWithoutExtension = dotNetAppHostExecutableNameWithoutExtension;
            Logger = logger;
        }

        public string AppHostPackPattern { get; private set; }
        public string AppHostKnownRuntimeIdentifiers { get; private set; }
        public string AppHostPackVersion { get; private set; }
        public string TargetingPackRoot { get; private set; }
        public RuntimeGraph RuntimeGraph { get; private set; }
        public string DotNetAppHostExecutableNameWithoutExtension { get; private set; }
        public Logger Logger { get; private set; }

        public (ITaskItem[] AppHost, List<TaskItem> AdditionalPackagesToDownload) GetAppHostItem(
           string appHostRuntimeIdentifier,
           string outputItemName)
        {
            var additionalPackagesToDownload = new List<TaskItem>();
            if (!String.IsNullOrEmpty(appHostRuntimeIdentifier) && !String.IsNullOrEmpty(AppHostPackPattern))
            {
                //  Choose AppHost RID as best match of the specified RID
                string bestAppHostRuntimeIdentifier = RuntimeGraph.GetBestRuntimeIdentifier(appHostRuntimeIdentifier, AppHostKnownRuntimeIdentifiers, out bool wasInGraph);

                if (bestAppHostRuntimeIdentifier == null)
                {
                    if (wasInGraph)
                    {
                        //  NETSDK1084: There was no app host for available for the specified RuntimeIdentifier '{0}'.
                        Logger.LogError(Strings.NoAppHostAvailable, appHostRuntimeIdentifier);
                    }
                    else
                    {
                        //  NETSDK1083: The specified RuntimeIdentifier '{0}' is not recognized.
                        Logger.LogError(Strings.UnsupportedRuntimeIdentifier, appHostRuntimeIdentifier);
                    }
                }
                else
                {
                    string appHostPackName = AppHostPackPattern.Replace("**RID**", bestAppHostRuntimeIdentifier);

                    string appHostRelativePathInPackage = Path.Combine("runtimes", bestAppHostRuntimeIdentifier, "native",
                        DotNetAppHostExecutableNameWithoutExtension +
                        ExecutableExtension.ForRuntimeIdentifier(bestAppHostRuntimeIdentifier));


                    TaskItem appHostItem = new TaskItem(outputItemName);
                    string appHostPackPath = null;
                    if (!String.IsNullOrEmpty(TargetingPackRoot))
                    {
                        appHostPackPath = Path.Combine(TargetingPackRoot, appHostPackName, AppHostPackVersion);
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
                        packageToDownload.SetMetadata(MetadataKeys.Version, AppHostPackVersion);
                        additionalPackagesToDownload.Add(packageToDownload);

                        appHostItem.SetMetadata(MetadataKeys.RuntimeIdentifier, appHostRuntimeIdentifier);
                        appHostItem.SetMetadata(MetadataKeys.PackageName, appHostPackName);
                        appHostItem.SetMetadata(MetadataKeys.PackageVersion, AppHostPackVersion);
                        appHostItem.SetMetadata(MetadataKeys.RelativePath, appHostRelativePathInPackage);
                    }

                    return (new ITaskItem[] { appHostItem }, additionalPackagesToDownload);
                }
            }

            return (null, additionalPackagesToDownload);
        }
    }
}
