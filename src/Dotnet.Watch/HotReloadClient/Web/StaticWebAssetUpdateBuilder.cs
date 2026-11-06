// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.DotNet.HotReload;

internal abstract class StaticWebAssetUpdateBuilder
{
    public readonly struct ProjectInstanceInfo
    {
        public required ProjectInstanceId Id { get; init; }
        public required bool HasScopedCssTargets { get; init; }
        public required string AssemblyName { get; init; }
    }

    private readonly Dictionary<ProjectInstanceId, Dictionary<string, StaticWebAsset>> _assets = [];
    private readonly HashSet<ProjectInstanceId> _projectInstancesToRegenerate = [];

    public IReadOnlyDictionary<ProjectInstanceId, Dictionary<string, StaticWebAsset>> Assets => _assets;

#if NET
    public IReadOnlySet<ProjectInstanceId> ProjectInstancesToRegenerate => _projectInstancesToRegenerate;
#else
    public IReadOnlyCollection<ProjectInstanceId> ProjectInstancesToRegenerate => _projectInstancesToRegenerate;
#endif

    protected abstract bool TryGetManifest(ProjectInstanceId id, [NotNullWhen(true)] out StaticWebAssetsManifest? manifest);
    protected abstract IEnumerable<ProjectInstanceInfo> GetProjectInstances(string projectPath);
    protected abstract IEnumerable<(ProjectInstanceInfo info, ILogger logger)> GetApplicationProjectAncestors(ProjectInstanceId projectInstanceId);

    public void AddAssets(
        string filePath,
        IEnumerable<string> containingProjectPaths,
        string? staticWebAssetRelativeUrl)
    {
        var isScopedCss = StaticWebAsset.IsScopedCssFile(filePath);
        if (!isScopedCss && staticWebAssetRelativeUrl is null)
        {
            return;
        }

        foreach (var containingProjectPath in containingProjectPaths)
        {
            foreach (var containingProjectInstanceInfo in GetProjectInstances(containingProjectPath))
            {
                if (isScopedCss)
                {
                    if (!containingProjectInstanceInfo.HasScopedCssTargets)
                    {
                        continue;
                    }

                    _projectInstancesToRegenerate.Add(containingProjectInstanceInfo.Id);
                }

                // Enumerate running application projects that transitively reference the project containing the changed asset.
                //
                // For regular asset files contained in Razor Class Libraries we could expect containingProjectPaths to have all the application projects that transitively reference the RCL
                // based on the asset manifest (these assets have _content-prefixed URLs). However, scoped CSS files in the RCLs are bundled into a single asset file.
                // They are not individually listed in the manifest so we can't map each individual asset's file path to its containing project based on the manifest.
                //
                // Instead, we only track files directly contained in their own project (Web App or RCL), i.e. the asset's containing project is only the RCL project,
                // and then enumerate the web application projects that transitively reference the containing project here.
                foreach (var (applicationProjectInstanceInfo, applicationProjectLogger) in GetApplicationProjectAncestors(containingProjectInstanceInfo.Id))
                {
                    string relativeUrl;

                    if (isScopedCss)
                    {
                        // Razor class library may be referenced by application that does not have static assets:
                        if (!applicationProjectInstanceInfo.HasScopedCssTargets)
                        {
                            continue;
                        }

                        _projectInstancesToRegenerate.Add(applicationProjectInstanceInfo.Id);

                        var bundleFileName = StaticWebAsset.GetScopedCssBundleFileName(
                            applicationProjectFilePath: applicationProjectInstanceInfo.Id.ProjectPath,
                            containingProjectFilePath: containingProjectInstanceInfo.Id.ProjectPath);

                        if (!TryGetManifest(applicationProjectInstanceInfo.Id, out var manifest))
                        {
                            // Shouldn't happen.
                            applicationProjectLogger.Log(LogEvents.StaticWebAssetManifestNotFound);
                            continue;
                        }

                        if (!manifest.TryGetBundleFilePath(bundleFileName, out var bundleFilePath))
                        {
                            // Shouldn't happen.
                            applicationProjectLogger.Log(LogEvents.ScopedCssBundleFileNotFound, bundleFileName);
                            continue;
                        }

                        filePath = bundleFilePath;
                        relativeUrl = bundleFileName;
                    }
                    else if (TryGetManifest(applicationProjectInstanceInfo.Id, out var _))
                    {
                        Debug.Assert(staticWebAssetRelativeUrl != null);
                        relativeUrl = staticWebAssetRelativeUrl;
                    }
                    else
                    {
                        // only refresh static web assets for web application projects (e.g. not for Aspire host app that references a web project)
                        continue;
                    }

                    if (!_assets.TryGetValue(applicationProjectInstanceInfo.Id, out var applicationAssets))
                    {
                        applicationAssets = [];
                        _assets.Add(applicationProjectInstanceInfo.Id, applicationAssets);
                    }
                    else if (applicationAssets.ContainsKey(filePath))
                    {
                        // asset already being updated in this application project:
                        continue;
                    }

                    applicationAssets.Add(filePath, new StaticWebAsset(
                        filePath,
                        StaticWebAsset.WebRoot + "/" + relativeUrl,
                        containingProjectInstanceInfo.AssemblyName,
                        isApplicationProject: containingProjectInstanceInfo.Id == applicationProjectInstanceInfo.Id));
                }
            }
        }
    }
}
