// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
        public ImmutableDictionary<ProjectId, AnalyzersAndFixers> GetAnalyzersAndFixers(
            Solution solution,
            FormatOptions formatOptions,
            ILogger logger)
        {
            if (!formatOptions.FixAnalyzers)
            {
                return ImmutableDictionary<ProjectId, AnalyzersAndFixers>.Empty;
            }

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

            try
            {
                var context = new AnalyzerLoadContext(Path.GetDirectoryName(path));

                // First try loading the assembly from disk.
                return context.LoadFromAssemblyPath(path);
            }
            catch { }

            // Give up.
            return null;
        }

        public DiagnosticSeverity GetSeverity(FormatOptions formatOptions) => formatOptions.AnalyzerSeverity;

        internal sealed class AnalyzerLoadContext : AssemblyLoadContext
        {
            private readonly string _assemblyFolderPath;

            public AnalyzerLoadContext(string assemblyFolderPath)
            {
                _assemblyFolderPath = assemblyFolderPath;
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                // Since we build against .NET Core 2.1 we do not have access to the
                // AssemblyDependencyResolver which resolves depenendency assembly paths
                // from AssemblyName by using the .deps.json.

                try
                {
                    // Search for assembly based on assembly name and culture within the analyzer folder.
                    var assembly = AssemblyResolver.TryResolveAssemblyFromPaths(this, assemblyName, _assemblyFolderPath);

                    if (assembly != null)
                    {
                        return assembly;
                    }
                }
                catch { }

                // Try to load the requested assembly from the default load context.
                return AssemblyLoadContext.Default.LoadFromAssemblyName(assemblyName);
            }
        }
    }
}
