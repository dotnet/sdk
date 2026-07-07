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
                    else
                    {
                        Debug.Assert(staticWebAssetRelativeUrl != null);
                        relativeUrl = staticWebAssetRelativeUrl;
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
