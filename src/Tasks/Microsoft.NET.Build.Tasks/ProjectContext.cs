﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.ProjectModel;

namespace Microsoft.NET.Build.Tasks
{
    public class ProjectContext
    {
        private readonly string _projectPath;
        private readonly LockFile _lockFile;
        private readonly LockFileTarget _lockFileTarget;

        public bool IsPortable { get; }

        public LockFile LockFile => _lockFile;
        public LockFileTarget LockFileTarget => _lockFileTarget;

        public ProjectContext(string projectPath, LockFile lockFile, LockFileTarget lockFileTarget)
        {
            _projectPath = projectPath;
            _lockFile = lockFile;
            _lockFileTarget = lockFileTarget;

            IsPortable = _lockFileTarget.IsPortable();
        }

        public IEnumerable<LockFileTargetLibrary> GetRuntimeLibraries(IEnumerable<string> privateAssetPackageIds)
        {
            IEnumerable<LockFileTargetLibrary> runtimeLibraries = _lockFileTarget.Libraries;
            Dictionary<string, LockFileTargetLibrary> libraryLookup =
                runtimeLibraries.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

            HashSet<string> allExclusionList = new HashSet<string>();

            if (IsPortable)
            {
                allExclusionList.UnionWith(_lockFileTarget.GetPlatformExclusionList(libraryLookup));
            }

            if (privateAssetPackageIds?.Any() == true)
            {
                HashSet<string> privateAssetsExclusionList =
                    GetPrivateAssetsExclusionList(
                        privateAssetPackageIds,
                        libraryLookup);

                allExclusionList.UnionWith(privateAssetsExclusionList);
            }

            return runtimeLibraries.Filter(allExclusionList).ToArray();
        }

        public IEnumerable<LockFileTargetLibrary> GetCompileLibraries(IEnumerable<string> compilePrivateAssetPackageIds)
        {
            IEnumerable<LockFileTargetLibrary> compileLibraries = _lockFileTarget.Libraries;

            if (compilePrivateAssetPackageIds?.Any() == true)
            {
                Dictionary<string, LockFileTargetLibrary> libraryLookup =
                    compileLibraries.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

                HashSet<string> privateAssetsExclusionList =
                    GetPrivateAssetsExclusionList(
                        compilePrivateAssetPackageIds,
                        libraryLookup);

                compileLibraries = compileLibraries.Filter(privateAssetsExclusionList);
            }

            return compileLibraries.ToArray();
        }

        public IEnumerable<string> GetTopLevelDependencies()
        {
            Dictionary<string, LockFileLibrary> libraryLookup =
                LockFile.Libraries.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);

            return LockFile
                .ProjectFileDependencyGroups
                .Where(dg => dg.FrameworkName == string.Empty ||
                             dg.FrameworkName == LockFileTarget.TargetFramework.DotNetFrameworkName)
                .SelectMany(g => g.Dependencies)
                .Select(projectFileDependency =>
                {
                    string libraryName = null;
                    int pathSeparatorIndex = projectFileDependency.IndexOfAny(new[] { '/', '\\' });
                    if (pathSeparatorIndex != -1)
                    {
                        // if projectFileDependency contains a path separator, then it isn't a valid
                        // library name.  Check to see if it is an MSBuild project
                        libraryName = FindMSBuildProjectLibraryName(projectFileDependency);
                    }

                    if (string.IsNullOrEmpty(libraryName))
                    {
                        int separatorIndex = projectFileDependency.IndexOf(' ');
                        libraryName = separatorIndex > 0 ?
                            projectFileDependency.Substring(0, separatorIndex) :
                            projectFileDependency;
                    }

                    if (!string.IsNullOrEmpty(libraryName) && libraryLookup.ContainsKey(libraryName))
                    {
                        return libraryName;
                    }

                    return null;
                })
                .Where(libraryName => libraryName != null)
                .ToArray();
        }

        private string FindMSBuildProjectLibraryName(string projectPath)
        {
            foreach (var library in LockFile.Libraries)
            {
                if (!string.IsNullOrEmpty(library.MSBuildProject))
                {
                    string fullDependencyProjectPath = Path.GetFullPath(Path.Combine(
                        Path.GetDirectoryName(_projectPath),
                        library.MSBuildProject));

                    if (fullDependencyProjectPath == projectPath)
                    {
                        return library.Name;
                    }
                }
            }

            return null;
        }

        public HashSet<string> GetPrivateAssetsExclusionList(
            IEnumerable<string> privateAssetPackageIds,
            IDictionary<string, LockFileTargetLibrary> libraryLookup)
        {
            var nonPrivateAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var nonPrivateAssetsToSearch = new Stack<string>();
            var privateAssetsToSearch = new Stack<string>();

            // Start with the top-level dependencies, and put them into "private" or "non-private" buckets
            var privateAssetPackagesLookup = new HashSet<string>(privateAssetPackageIds, StringComparer.OrdinalIgnoreCase);
            foreach (var topLevelDependency in GetTopLevelDependencies())
            {
                if (!privateAssetPackagesLookup.Contains(topLevelDependency))
                {
                    nonPrivateAssetsToSearch.Push(topLevelDependency);
                    nonPrivateAssets.Add(topLevelDependency);
                }
                else
                {
                    privateAssetsToSearch.Push(topLevelDependency);
                }
            }

            LockFileTargetLibrary library;
            string libraryName;

            // Walk all the non-private assets' dependencies and mark them as non-private
            while (nonPrivateAssetsToSearch.Count > 0)
            {
                libraryName = nonPrivateAssetsToSearch.Pop();
                if (libraryLookup.TryGetValue(libraryName, out library))
                {
                    foreach (var dependency in library.Dependencies)
                    {
                        if (!nonPrivateAssets.Contains(dependency.Id))
                        {
                            nonPrivateAssetsToSearch.Push(dependency.Id);
                            nonPrivateAssets.Add(dependency.Id);
                        }
                    }
                }
            }

            // Go through assets marked private and their dependencies
            // For libraries not marked as non-private, mark them down as private
            var privateAssetsToExclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (privateAssetsToSearch.Count > 0)
            {
                libraryName = privateAssetsToSearch.Pop();
                if (libraryLookup.TryGetValue(libraryName, out library))
                {
                    privateAssetsToExclude.Add(libraryName);

                    foreach (var dependency in library.Dependencies)
                    {
                        if (!nonPrivateAssets.Contains(dependency.Id))
                        {
                            privateAssetsToSearch.Push(dependency.Id);
                        }
                    }
                }
            }

            return privateAssetsToExclude;
        }
    }
}
