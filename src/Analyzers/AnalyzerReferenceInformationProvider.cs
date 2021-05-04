// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis.Tools.Utilities;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal class AnalyzerReferenceInformationProvider : IAnalyzerInformationProvider
    {
        private static readonly Dictionary<string, Assembly> s_pathsToAssemblies = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Assembly> s_namesToAssemblies = new(StringComparer.OrdinalIgnoreCase);

        private static readonly object s_guard = new();

        public ImmutableDictionary<ProjectId, AnalyzersAndFixers> GetAnalyzersAndFixers(
            Solution solution,
            FormatOptions formatOptions,
            ILogger logger)
        {
            return solution.Projects
                .ToImmutableDictionary(project => project.Id, GetAnalyzersAndFixers);
        }

        private AnalyzersAndFixers GetAnalyzersAndFixers(Project project)
        {
            var analyzerAssemblies = project.AnalyzerReferences
                .Select(reference => TryLoadAssemblyFrom(reference.FullPath))
                .OfType<Assembly>()
                .ToImmutableArray();

            return AnalyzerFinderHelpers.LoadAnalyzersAndFixers(analyzerAssemblies);
        }

        private Assembly? TryLoadAssemblyFrom(string? path)
        {
            // Since we are not deploying these assemblies we need to ensure the files exist.
            if (path is null || !File.Exists(path))
            {
                return null;
            }

            lock (s_guard)
            {
                if (s_pathsToAssemblies.TryGetValue(path, out var cachedAssembly))
                {
                    return cachedAssembly;
                }

                try
                {
                    var context = new AnalyzerLoadContext(path);
                    var assembly = context.LoadFromAssemblyPath(path);

                    s_pathsToAssemblies.Add(path, assembly);

                    return assembly;
                }
                catch { }
            }

            return null;
        }

        public DiagnosticSeverity GetSeverity(FormatOptions formatOptions) => formatOptions.AnalyzerSeverity;

        internal sealed class AnalyzerLoadContext : AssemblyLoadContext
        {
            internal string AssemblyFolderPath { get; }
            internal AssemblyDependencyResolver DependencyResolver { get; }

            public AnalyzerLoadContext(string assemblyPath)
            {
                var analyzerDirectory = new DirectoryInfo(Path.GetDirectoryName(assemblyPath));

                // Analyzer packages will put language specific assemblies in subfolders.
                if (analyzerDirectory.Name == "cs" || analyzerDirectory.Name == "vb")
                {
                    // Get the root analyzer folder.
                    analyzerDirectory = analyzerDirectory.Parent;
                }

                AssemblyFolderPath = analyzerDirectory.FullName;
                DependencyResolver = new AssemblyDependencyResolver(assemblyPath);
            }

            protected override Assembly? Load(AssemblyName assemblyName)
            {
                if (s_namesToAssemblies.TryGetValue(assemblyName.FullName, out var assembly))
                {
                    return assembly;
                }

                assembly = TryLoad(assemblyName);
                if (assembly is not null)
                {
                    s_namesToAssemblies[assemblyName.FullName] = assembly;
                }

                return assembly;
            }

            private Assembly? TryLoad(AssemblyName assemblyName)
            {
                // If the analyzer was packaged with a .deps.json file which described
                // its dependencies, then the DependencyResolver should locate them for us.
                var resolvedPath = DependencyResolver.ResolveAssemblyToPath(assemblyName);
                if (resolvedPath is not null)
                {
                    return LoadFromAssemblyPath(resolvedPath);
                }

                // The dependency resolver failed to locate the dependency so fall back to inspecting
                // the analyzer package folder.
                foreach (var searchPath in
                    new[]
                    {
                        AssemblyFolderPath,
                        Path.Combine(AssemblyFolderPath, "cs"),
                        Path.Combine(AssemblyFolderPath, "vb")
                    })
                {
                    try
                    {
                        // Search for assembly based on assembly name and culture within the analyzer folder.
                        var assembly = AssemblyResolver.TryResolveAssemblyFromPaths(this, assemblyName, searchPath);

                        if (assembly != null)
                        {
                            return assembly;
                        }
                    }
                    catch { }
                }

                return null;
            }

            protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
            {
                // If the analyzer was packaged with a .deps.json file which described
                // its dependencies, then the DependencyResolver should locate them for us.
                var resolvedPath = DependencyResolver.ResolveUnmanagedDllToPath(unmanagedDllName);
                if (resolvedPath is not null)
                {
                    return LoadUnmanagedDllFromPath(resolvedPath);
                }

                return base.LoadUnmanagedDll(unmanagedDllName);
            }
        }
    }
}
