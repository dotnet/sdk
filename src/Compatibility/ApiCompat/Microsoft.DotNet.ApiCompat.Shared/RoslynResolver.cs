// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.Loader;

namespace Microsoft.DotNet.ApiCompat
{
    /// <summary>
    /// Helper to resolve the Microsoft.CodeAnalysis roslyn assemblies based on a given assemblies path.
    /// </summary>
    internal sealed class RoslynResolver
    {
        private readonly string _roslynAssembliesPath;
        private readonly AssemblyLoadContext? _currentContext;

        private RoslynResolver(string roslynAssembliesPath)
        {
            _roslynAssembliesPath = roslynAssembliesPath;

            _currentContext = AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly());
            if (_currentContext != null)
            {
                _currentContext.Resolving += Resolve;
            }
        }

        public static RoslynResolver Register(string roslynAssembliesPath) => new(roslynAssembliesPath);

        public void Unregister()
        {
            if (_currentContext != null)
            {
                _currentContext.Resolving -= Resolve;
            }
        }

        private Assembly? Resolve(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            return LoadRoslyn(assemblyName, path => context.LoadFromAssemblyPath(path));
        }

        private Assembly? LoadRoslyn(AssemblyName name, Func<string, Assembly> loadFromPath)
        {
            const string codeAnalysisName = "Microsoft.CodeAnalysis";
            const string codeAnalysisCSharpName = "Microsoft.CodeAnalysis.CSharp";

            if (name.Name == codeAnalysisName || name.Name == codeAnalysisCSharpName)
            {
                Assembly asm = loadFromPath(Path.Combine(_roslynAssembliesPath!, $"{name.Name}.dll"));
                Version? resolvedVersion = asm.GetName().Version;
                if (resolvedVersion < name.Version)
                {
                    throw new Exception(string.Format(CommonResources.UpdateSdkVersion, resolvedVersion, name.Version));
                }

                // Being extra defensive but we want to avoid that we accidentally load two different versions of either
                // of the roslyn assemblies from a different location, so let's load them both on the first request.
                if (name.Name == codeAnalysisName)
                    loadFromPath(Path.Combine(_roslynAssembliesPath!, $"{codeAnalysisCSharpName}.dll"));
                else
                    loadFromPath(Path.Combine(_roslynAssembliesPath!, $"{codeAnalysisName}.dll"));

                return asm;
            }

            return null;
        }
    }
}
