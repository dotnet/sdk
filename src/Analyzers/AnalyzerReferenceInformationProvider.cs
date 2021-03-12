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
                    var analyzerDirectory = new DirectoryInfo(Path.GetDirectoryName(path));

                    // Analyzer packages will put language specific assemblies in subfolders.
                    if (analyzerDirectory.Name == "cs" || analyzerDirectory.Name == "vb")
                    {
                        // Get the root analyzer folder.
                        analyzerDirectory = analyzerDirectory.Parent;
                    }

                    var context = new AnalyzerLoadContext(analyzerDirectory.FullName);

                    // First try loading the assembly from disk.
                    var assembly = context.LoadFromAssemblyPath(path);

                    s_pathsToAssemblies.Add(path, assembly);

                    return assembly;
                }
                catch { }
            }

            // Give up.
            return null;
        }

        public DiagnosticSeverity GetSeverity(FormatOptions formatOptions) => formatOptions.AnalyzerSeverity;

        internal sealed class AnalyzerLoadContext : AssemblyLoadContext
        {
            internal string AssemblyFolderPath { get; }

            public AnalyzerLoadContext(string assemblyFolderPath)
            {
                AssemblyFolderPath = assemblyFolderPath;
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                // Since we build against .NET Core 2.1 we do not have access to the
                // AssemblyDependencyResolver which resolves depenendency assembly paths
                // from AssemblyName by using the .deps.json.

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

                // Try to load the requested assembly from the default load context.
                return AssemblyLoadContext.Default.LoadFromAssemblyName(assemblyName);
            }
        }
    }
}
