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
            _appHostPackPattern = appHostPackPattern;
            _appHostKnownRuntimeIdentifiers = appHostKnownRuntimeIdentifiers;
            _appHostPackVersion = appHostPackVersion;
            _targetingPackRoot = targetingPackRoot;
            _runtimeGraph = runtimeGraph;
            _dotNetAppHostExecutableNameWithoutExtension = dotNetAppHostExecutableNameWithoutExtension;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private readonly string _appHostPackPattern;
        private readonly string _appHostKnownRuntimeIdentifiers;
        private readonly string _appHostPackVersion;
        private readonly string _targetingPackRoot;
        private readonly RuntimeGraph _runtimeGraph;
        private readonly string _dotNetAppHostExecutableNameWithoutExtension;
        private readonly Logger _logger;

        public (ITaskItem[] AppHost, List<TaskItem> AdditionalPackagesToDownload) GetAppHostItem(
           string appHostRuntimeIdentifier,
           string outputItemName)
        {
            var additionalPackagesToDownload = new List<TaskItem>();
            if (!String.IsNullOrEmpty(appHostRuntimeIdentifier) && !String.IsNullOrEmpty(_appHostPackPattern))
            {
                //  Choose AppHost RID as best match of the specified RID
                string bestAppHostRuntimeIdentifier =
                    _runtimeGraph.GetBestRuntimeIdentifier(
                        appHostRuntimeIdentifier,
                        _appHostKnownRuntimeIdentifiers,
                        out bool wasInGraph);

                if (bestAppHostRuntimeIdentifier == null)
                {
                    if (wasInGraph)
                    {
                        //  NETSDK1084: There was no app host for available for the specified RuntimeIdentifier '{0}'.
                        _logger.LogError(Strings.NoAppHostAvailable, appHostRuntimeIdentifier);
                    }
                    else
                    {
                        //  NETSDK1083: The specified RuntimeIdentifier '{0}' is not recognized.
                        _logger.LogError(Strings.UnsupportedRuntimeIdentifier, appHostRuntimeIdentifier);
                    }
                }
                else
                {
                    string appHostPackName =
                        _appHostPackPattern.Replace("**RID**", bestAppHostRuntimeIdentifier);

                    string appHostRelativePathInPackage =
                        Path.Combine("runtimes",
                        bestAppHostRuntimeIdentifier, "native",
                        _dotNetAppHostExecutableNameWithoutExtension +
                        ExecutableExtension.ForRuntimeIdentifier(bestAppHostRuntimeIdentifier));

                    TaskItem appHostItem = new TaskItem(outputItemName);
                    string appHostPackPath = null;
                    if (!String.IsNullOrEmpty(_targetingPackRoot))
                    {
                        appHostPackPath = Path.Combine(_targetingPackRoot, appHostPackName, _appHostPackVersion);
                    }

                    if (appHostPackPath != null && Directory.Exists(appHostPackPath))
                    {
                        //  Use AppHost from packs folder
                        appHostItem.SetMetadata(
                            MetadataKeys.Path,
                            Path.Combine(appHostPackPath, appHostRelativePathInPackage));
                    }
                    else
                    {
                        //  Download apphost pack
                        TaskItem packageToDownload = new TaskItem(appHostPackName);
                        packageToDownload.SetMetadata(MetadataKeys.Version, _appHostPackVersion);
                        additionalPackagesToDownload.Add(packageToDownload);

                        appHostItem.SetMetadata(MetadataKeys.RuntimeIdentifier, appHostRuntimeIdentifier);
                        appHostItem.SetMetadata(MetadataKeys.PackageName, appHostPackName);
                        appHostItem.SetMetadata(MetadataKeys.PackageVersion, _appHostPackVersion);
                        appHostItem.SetMetadata(MetadataKeys.RelativePath, appHostRelativePathInPackage);
                    }

                    return (new ITaskItem[] { appHostItem }, additionalPackagesToDownload);
                }
            }

            return (null, additionalPackagesToDownload);
        }
    }
}
