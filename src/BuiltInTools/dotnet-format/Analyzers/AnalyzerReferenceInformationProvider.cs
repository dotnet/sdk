// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Host;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal class AnalyzerReferenceInformationProvider : IAnalyzerInformationProvider
    {
        private static readonly Dictionary<string, Assembly> s_pathsToAssemblies = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Assembly> s_namesToAssemblies = new(StringComparer.OrdinalIgnoreCase);

        private static readonly object s_guard = new();

        public ImmutableDictionary<ProjectId, AnalyzersAndFixers> GetAnalyzersAndFixers(
            Workspace workspace,
            Solution solution,
            FormatOptions formatOptions,
            ILogger logger)
        {
            return solution.Projects
                .ToImmutableDictionary(project => project.Id, project => GetAnalyzersAndFixers(workspace, project));
        }

        private static AnalyzersAndFixers GetAnalyzersAndFixers(Workspace workspace, Project project)
        {
            var analyzerAssemblies = project.AnalyzerReferences
                .Select(reference => TryLoadAssemblyFrom(workspace, reference.FullPath, reference))
                .OfType<Assembly>()
                .ToImmutableArray();

            var analyzers = project.AnalyzerReferences.SelectMany(reference => reference.GetAnalyzers(project.Language)).ToImmutableArray();
            return new AnalyzersAndFixers(analyzers, AnalyzerFinderHelpers.LoadFixers(analyzerAssemblies, project.Language));
        }

        private static Assembly? TryLoadAssemblyFrom(Workspace workspace, string? path, AnalyzerReference analyzerReference)
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
                    Assembly analyzerAssembly;
                    if (analyzerReference is AnalyzerFileReference analyzerFileReference)
                    {
                        // If we have access to the analyzer file reference, we can update our
                        // cache and return the assembly.
                        analyzerAssembly = analyzerFileReference.GetAssembly();
                        s_namesToAssemblies.TryAdd(analyzerAssembly.GetName().FullName, analyzerAssembly);
                    }
                    else
                    {
                        var analyzerService = workspace.Services.GetService<IAnalyzerService>() ?? throw new NotSupportedException();
                        var loader = analyzerService.GetLoader();
                        analyzerAssembly = loader.LoadFromPath(path);
                    }

                    s_pathsToAssemblies.Add(path, analyzerAssembly);

                    return analyzerAssembly;
                }
                catch
                {
                }
            }

            return null;
        }

        public DiagnosticSeverity GetSeverity(FormatOptions formatOptions) => formatOptions.AnalyzerSeverity;
    }
}
