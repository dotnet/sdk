// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.IDE
{
    internal class Installer : IInstaller
    {
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly Paths _paths;

        public Installer(IEngineEnvironmentSettings environmentSettings)
        {
            _environmentSettings = environmentSettings;
            _paths = new Paths(environmentSettings);
        }

        public void InstallPackages(IEnumerable<string> installationRequests)
        {
            List<string> localSources = new List<string>();
            InstallLocalPackages(installationRequests.Where(x => x != null));
        }

        public IEnumerable<string> Uninstall(IEnumerable<string> uninstallRequests)
        {
            List<string> uninstallFailures = new List<string>();
            foreach (string uninstall in uninstallRequests)
            {
                string prefix = Path.Combine(_paths.User.Packages, uninstall);
                IReadOnlyList<MountPointInfo> rootMountPoints = _environmentSettings.SettingsLoader.MountPoints.Where(x =>
                {
                    if (x.ParentMountPointId != Guid.Empty)
                    {
                        return false;
                    }

                    if (uninstall.IndexOfAny(new[] { '/', '\\' }) < 0)
                    {
                        if (x.Place.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase) && x.Place.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }

                    if (string.Equals(x.Place, uninstall, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    else if (x.Place.Length > uninstall.Length)
                    {
                        string place = x.Place.Replace('\\', '/');
                        string match = uninstall.Replace('\\', '/');

                        if (match[match.Length - 1] != '/')
                        {
                            match += "/";
                        }

                        return place.StartsWith(match, StringComparison.OrdinalIgnoreCase);
                    }

                    return false;
                }).ToList();

                if (rootMountPoints.Count == 0)
                {
                    uninstallFailures.Add(uninstall);
                    continue;
                }

                HashSet<Guid> mountPoints = new HashSet<Guid>(rootMountPoints.Select(x => x.MountPointId));
                bool isSearchComplete = false;
                while (!isSearchComplete)
                {
                    isSearchComplete = true;
                    foreach (MountPointInfo possibleChild in _environmentSettings.SettingsLoader.MountPoints)
                    {
                        if (mountPoints.Contains(possibleChild.ParentMountPointId))
                        {
                            isSearchComplete &= !mountPoints.Add(possibleChild.MountPointId);
                        }
                    }
                }

                //Find all of the things that refer to any of the mount points we've got
                _environmentSettings.SettingsLoader.RemoveMountPoints(mountPoints);
                _environmentSettings.SettingsLoader.Save();

                foreach (MountPointInfo mountPoint in rootMountPoints)
                {
                    if (mountPoint.Place.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var attributes = _environmentSettings.Host.FileSystem.GetFileAttributes(mountPoint.Place);
                            if (attributes.HasFlag(FileAttributes.ReadOnly))
                            {
                                attributes &= ~FileAttributes.ReadOnly;
                                _environmentSettings.Host.FileSystem.SetFileAttributes(mountPoint.Place, attributes);
                            }

                            _environmentSettings.Host.FileSystem.FileDelete(mountPoint.Place);
                        }
                        catch
                        {
                        }
                    }
                }
            }

            return uninstallFailures;
        }

        private void InstallLocalPackages(IEnumerable<string> packageNames)
        {
            List<string> toInstall = new List<string>();

            foreach (string package in packageNames)
            {
                string pkg = package.Trim();
                pkg = _environmentSettings.Environment.ExpandEnvironmentVariables(pkg);
                string pattern = null;

                int wildcardIndex = pkg.IndexOfAny(new[] { '*', '?' });

                if (wildcardIndex > -1)
                {
                    int lastSlashBeforeWildcard = pkg.LastIndexOfAny(new[] { '\\', '/' });
                    pattern = pkg.Substring(lastSlashBeforeWildcard + 1);
                    pkg = pkg.Substring(0, lastSlashBeforeWildcard);
                }

                try
                {
                    if (pattern != null)
                    {
                        string fullDirectory = new DirectoryInfo(pkg).FullName;
                        string fullPathGlob = Path.Combine(fullDirectory, pattern);
                        ((SettingsLoader)_environmentSettings.SettingsLoader).UserTemplateCache.Scan(fullPathGlob);
                    }
                    else if (_environmentSettings.Host.FileSystem.DirectoryExists(pkg) || _environmentSettings.Host.FileSystem.FileExists(pkg))
                    {
                        string packageLocation = new DirectoryInfo(pkg).FullName;
                        ((SettingsLoader)_environmentSettings.SettingsLoader).UserTemplateCache.Scan(packageLocation);
                    }
                    else
                    {
                        _environmentSettings.Host.OnNonCriticalError("InvalidPackageSpecification", pkg, null, 0);
                    }
                }
                catch
                {
                    _environmentSettings.Host.OnNonCriticalError("InvalidPackageSpecification", pkg, null, 0);
                }
            }

            _environmentSettings.SettingsLoader.Save();
        }
    }
}
