// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Microsoft.CodeAnalysis
{
    /// <summary>
    /// The base implementation for <see cref="IAnalyzerAssemblyLoader"/>. This type provides caching and tracking of inputs given
    /// to <see cref="AddDependencyLocation(string)"/>.
    /// </summary>
    /// <remarks>
    /// This type generally assumes that files on disk aren't changing, since it ensure that two calls to <see cref="LoadFromPath(string)"/>
    /// will always return the same thing, per that interface's contract.
    /// </remarks>
    internal abstract class AnalyzerAssemblyLoader : IAnalyzerAssemblyLoader
    {
        private readonly object _guard = new();

        // lock _guard to read/write
        private readonly Dictionary<string, Assembly> _loadedAssembliesByPath = new();

        // maps file name to a full path (lock _guard to read/write):
        private readonly Dictionary<string, ImmutableHashSet<string>> _knownAssemblyPathsBySimpleName = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Implemented by derived types to actually perform the load for an assembly that doesn't have a cached result.
        /// </summary>
        protected abstract Assembly LoadFromPathUncheckedImpl(string fullPath);

        #region Public API

        public void AddDependencyLocation(string fullPath)
        {
            Debug.Assert(Path.IsPathFullyQualified(fullPath));
            var simpleName = Path.GetFileName(fullPath);

            lock (_guard)
            {
                if (!_knownAssemblyPathsBySimpleName.TryGetValue(simpleName, out var paths))
                {
                    paths = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, fullPath);
                    _knownAssemblyPathsBySimpleName.Add(simpleName, paths);
                }
                else
                {
                    _knownAssemblyPathsBySimpleName[simpleName] = paths.Add(fullPath);
                }
            }
        }

        public Assembly LoadFromPath(string fullPath)
        {
            Debug.Assert(Path.IsPathFullyQualified(fullPath));
            return LoadFromPathUnchecked(fullPath);
        }

        #endregion

        /// <summary>
        /// Returns the cached assembly for fullPath if we've done a load for this path before, or calls <see cref="LoadFromPathUncheckedImpl"/> if
        /// it needs to be loaded. This method skips the check in release builds that the path is an absolute path, hence the "Unchecked" in the name.
        /// </summary>
        protected Assembly LoadFromPathUnchecked(string fullPath)
        {
            Debug.Assert(Path.IsPathFullyQualified(fullPath));

            // Check if we have already loaded an assembly from the given path.
            Assembly? loadedAssembly = null;
            lock (_guard)
            {
                if (_loadedAssembliesByPath.TryGetValue(fullPath, out var existingAssembly))
                {
                    loadedAssembly = existingAssembly;
                }
            }

            // Otherwise, load the assembly.
            if (loadedAssembly == null)
            {
                loadedAssembly = LoadFromPathUncheckedImpl(fullPath);
            }

            // Add the loaded assembly to the path cache.
            lock (_guard)
            {
                _loadedAssembliesByPath[fullPath] = loadedAssembly;
            }

            return loadedAssembly;
        }

        protected ImmutableHashSet<string>? GetPaths(string simpleName)
        {
            lock (_guard)
            {
                _knownAssemblyPathsBySimpleName.TryGetValue(simpleName, out var paths);
                return paths;
            }
        }

        /// <summary>
        /// When overridden in a derived class, allows substituting an assembly path after we've
        /// identified the context to load an assembly in, but before the assembly is actually
        /// loaded from disk. This is used to substitute out the original path with the shadow-copied version.
        /// </summary>
        protected virtual string GetPathToLoad(string fullPath)
        {
            return fullPath;
        }
    }
}
