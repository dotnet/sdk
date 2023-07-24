// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Microsoft.CodeAnalysis
{
    internal class DefaultAnalyzerAssemblyLoader : AnalyzerAssemblyLoader
    {
        /// <summary>
        /// <p>Typically a user analyzer has a reference to the compiler and some of the compiler's
        /// dependencies such as System.Collections.Immutable. For the analyzer to correctly
        /// interoperate with the compiler that created it, we need to ensure that we always use the
        /// compiler's version of a given assembly over the analyzer's version.</p>
        ///
        /// <p>If we neglect to do this, then in the case where the user ships the compiler or its
        /// dependencies in the analyzer's bin directory, we could end up loading a separate
        /// instance of those assemblies in the process of loading the analyzer, which will surface
        /// as a failure to load the analyzer.</p>
        /// </summary>
        internal static readonly ImmutableHashSet<string> CompilerAssemblySimpleNames =
           ImmutableHashSet.Create(
               StringComparer.OrdinalIgnoreCase,
               "Microsoft.CodeAnalysis",
               "Microsoft.CodeAnalysis.CSharp",
               "Microsoft.CodeAnalysis.VisualBasic",
               "System.Collections",
               "System.Collections.Concurrent",
               "System.Collections.Immutable",
               "System.Console",
               "System.Diagnostics.Debug",
               "System.Diagnostics.StackTrace",
               "System.Diagnostics.Tracing",
               "System.IO.Compression",
               "System.IO.FileSystem",
               "System.Linq",
               "System.Linq.Expressions",
               "System.Memory",
               "System.Reflection.Metadata",
               "System.Reflection.Primitives",
               "System.Resources.ResourceManager",
               "System.Runtime",
               "System.Runtime.CompilerServices.Unsafe",
               "System.Runtime.Extensions",
               "System.Runtime.InteropServices",
               "System.Runtime.InteropServices.RuntimeInformation",
               "System.Runtime.Loader",
               "System.Runtime.Numerics",
               "System.Runtime.Serialization.Primitives",
               "System.Security.Cryptography.Algorithms",
               "System.Security.Cryptography.Primitives",
               "System.Text.Encoding.CodePages",
               "System.Text.Encoding.Extensions",
               "System.Text.RegularExpressions",
               "System.Threading",
               "System.Threading.Tasks",
               "System.Threading.Tasks.Parallel",
               "System.Threading.Thread",
               "System.Threading.ThreadPool",
               "System.Xml.ReaderWriter",
               "System.Xml.XDocument",
               "System.Xml.XPath.XDocument");

        internal virtual ImmutableHashSet<string> AssemblySimpleNamesToBeLoadedInCompilerContext => CompilerAssemblySimpleNames;

        // This is the context where compiler (and some of its dependencies) are being loaded into, which might be different from AssemblyLoadContext.Default.
        private static readonly AssemblyLoadContext s_compilerLoadContext = AssemblyLoadContext.GetLoadContext(typeof(DefaultAnalyzerAssemblyLoader).GetTypeInfo().Assembly)!;

        private readonly object _guard = new();
        private readonly Dictionary<string, AnalyzerLoadContext> _loadContextByAnalyzer = new(StringComparer.Ordinal);

        protected override Assembly LoadFromPathUncheckedImpl(string fullPath)
        {
            AnalyzerLoadContext? loadContext;

            var fullDirectoryPath = Path.GetDirectoryName(fullPath) ?? throw new ArgumentException(message: null, paramName: nameof(fullPath));
            lock (_guard)
            {
                if (!_loadContextByAnalyzer.TryGetValue(fullPath, out loadContext))
                {
                    var dependencyResolver = new AssemblyDependencyResolver(fullPath);
                    loadContext = new AnalyzerLoadContext(fullDirectoryPath, this, s_compilerLoadContext, dependencyResolver);
                    _loadContextByAnalyzer[fullPath] = loadContext;
                }
            }

            var name = AssemblyName.GetAssemblyName(fullPath);
            return loadContext.LoadFromAssemblyName(name);
        }

        private sealed class AnalyzerLoadContext : AssemblyLoadContext
        {
            internal string Directory { get; }
            private readonly DefaultAnalyzerAssemblyLoader _loader;
            private readonly AssemblyLoadContext _compilerLoadContext;
            private readonly AssemblyDependencyResolver _dependencyResolver;

            public AnalyzerLoadContext(string directory, DefaultAnalyzerAssemblyLoader loader, AssemblyLoadContext compilerLoadContext, AssemblyDependencyResolver dependencyResolver)
            {
                Directory = directory;
                _loader = loader;
                _compilerLoadContext = compilerLoadContext;
                _dependencyResolver = dependencyResolver;
            }

            protected override Assembly? Load(AssemblyName assemblyName)
            {
                var simpleName = assemblyName.Name!;
                if (_loader.AssemblySimpleNamesToBeLoadedInCompilerContext.Contains(simpleName))
                {
                    // Delegate to the compiler's load context to load the compiler or anything
                    // referenced by the compiler
                    return _compilerLoadContext.LoadFromAssemblyName(assemblyName);
                }

                // If the analyzer was packaged with a .deps.json file which described
                // its dependencies, then the DependencyResolver should locate them for us.
                var resolvedPath = _dependencyResolver.ResolveAssemblyToPath(assemblyName);
                if (resolvedPath is not null)
                {
                    return LoadFromAssemblyPath(resolvedPath);
                }

                var assemblyPath = Path.Combine(Directory, simpleName + ".dll");
                var paths = _loader.GetPaths(simpleName);
                if (paths is null)
                {
                    // The analyzer didn't explicitly register this dependency. Most likely the
                    // assembly we're trying to load here is netstandard or a similar framework
                    // assembly. In this case, we want to load it in compiler's ALC to avoid any
                    // potential type mismatch issue. Otherwise, if this is truly an unknown assembly,
                    // we assume both compiler and default ALC will fail to load it.
                    return _compilerLoadContext.LoadFromAssemblyName(assemblyName);
                }

                Debug.Assert(paths.Any());
                // A matching assembly in this directory was specified via /analyzer.
                if (paths.Contains(assemblyPath))
                {
                    return LoadFromAssemblyPath(_loader.GetPathToLoad(assemblyPath));
                }

                AssemblyName? bestCandidateName = null;
                string? bestCandidatePath = null;
                // The assembly isn't expected to be found at 'assemblyPath',
                // but some assembly with the same simple name is known to the loader.
                foreach (var candidatePath in paths)
                {
                    // Note: we assume that the assembly really can be found at 'candidatePath'
                    // (without 'GetPathToLoad'), and that calling GetAssemblyName doesn't cause us
                    // to hold a lock on the file. This prevents unnecessary shadow copies.
                    var candidateName = AssemblyName.GetAssemblyName(candidatePath);
                    // Checking FullName ensures that version and PublicKeyToken match exactly.
                    if (candidateName.FullName.Equals(assemblyName.FullName, StringComparison.OrdinalIgnoreCase))
                    {
                        return LoadFromAssemblyPath(_loader.GetPathToLoad(candidatePath));
                    }
                    else if (bestCandidateName is null || bestCandidateName.Version < candidateName.Version)
                    {
                        bestCandidateName = candidateName;
                        bestCandidatePath = candidatePath;
                    }
                }

                Debug.Assert(bestCandidateName != null);
                Debug.Assert(bestCandidatePath != null);

                return LoadFromAssemblyPath(_loader.GetPathToLoad(bestCandidatePath));
            }

            protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
            {
                // If the analyzer was packaged with a .deps.json file which described
                // its dependencies, then the DependencyResolver should locate them for us.
                var resolvedPath = _dependencyResolver.ResolveUnmanagedDllToPath(unmanagedDllName);
                if (resolvedPath is not null)
                {
                    return LoadUnmanagedDllFromPath(resolvedPath);
                }

                var assemblyPath = Path.Combine(Directory, unmanagedDllName + ".dll");
                var paths = _loader.GetPaths(unmanagedDllName);
                if (paths is null || !paths.Contains(assemblyPath))
                {
                    return IntPtr.Zero;
                }

                var pathToLoad = _loader.GetPathToLoad(assemblyPath);
                return LoadUnmanagedDllFromPath(pathToLoad);
            }
        }
    }
}
