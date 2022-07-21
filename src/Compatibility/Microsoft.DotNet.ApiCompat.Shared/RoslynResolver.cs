// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
#if NETCOREAPP
using System.Runtime.Loader;
#endif

namespace Microsoft.DotNet.ApiCompat
{
    /// <summary>
    /// Helper to resolve the Microsoft.CodeAnalysis roslyn assemblies based on a given assemblies path.
    /// </summary>
    internal static class RoslynResolver
    {
        private static string? s_roslynAssembliesPath;
#if NETCOREAPP
        private static AssemblyLoadContext? s_currentContext;
#endif

        public static void Register(string? roslynAssembliesPath)
        {
            if (roslynAssembliesPath == null)
                return;

            s_roslynAssembliesPath = roslynAssembliesPath;

#if NETCOREAPP
            s_currentContext = AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly())!;
            s_currentContext.Resolving += RoslynResolver.Resolve;
#else
            AppDomain.CurrentDomain.AssemblyResolve += RoslynResolver.Resolve;
#endif
        }

        public static void Unregister()
        {
#if NETCOREAPP
            if (s_currentContext is null)
            {
                throw new InvalidOperationException("The RoslynResolver must be registered first.");
            }

            s_currentContext.Resolving -= RoslynResolver.Resolve;
#else
            AppDomain.CurrentDomain.AssemblyResolve -= RoslynResolver.Resolve;
#endif
        }

#if NETCOREAPP
        private static Assembly? Resolve(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            return LoadRoslyn(assemblyName, path => context.LoadFromAssemblyPath(path));
        }
#else
        private static Assembly? Resolve(object sender, ResolveEventArgs args)
        {
            AssemblyName name = new(args.Name);
            return LoadRoslyn(name, path => Assembly.LoadFrom(path));
        }
#endif

        private static Assembly? LoadRoslyn(AssemblyName name, Func<string, Assembly> loadFromPath)
        {
            const string codeAnalysisName = "Microsoft.CodeAnalysis";
            const string codeAnalysisCsharpName = "Microsoft.CodeAnalysis.CSharp";

            if (name.Name == codeAnalysisName || name.Name == codeAnalysisCsharpName)
            {
                Assembly asm = loadFromPath(Path.Combine(s_roslynAssembliesPath!, $"{name.Name}.dll"));
                Version? resolvedVersion = asm.GetName().Version;
                if (resolvedVersion < name.Version)
                {
                    throw new Exception(string.Format(CommonResources.UpdateSdkVersion, resolvedVersion, name.Version));
                }

                // Being extra defensive but we want to avoid that we accidentally load two different versions of either
                // of the roslyn assemblies from a different location, so let's load them both on the first request.
                if (name.Name == codeAnalysisName)
                    loadFromPath(Path.Combine(s_roslynAssembliesPath!, $"{codeAnalysisCsharpName}.dll"));
                else
                    loadFromPath(Path.Combine(s_roslynAssembliesPath!, $"{codeAnalysisName}.dll"));

                return asm;
            }

            return null;
        }
    }
}
