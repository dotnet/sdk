// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET
using System.Text.RegularExpressions;
#endif
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions;

namespace Microsoft.DotNet.GenAPI
{
    /// <summary>
    /// Class to standardize initialization and running of GenAPI tool.
    /// Shared between CLI and MSBuild tasks frontends.
    /// </summary>
    public static class GenAPIApp
    {
        /// <summary>
        /// Initialize and run Roslyn-based GenAPI tool.
        /// </summary>
        public static void Run(GenAPIConfiguration config)
        {
            // Invoke an assembly symbol writer for each directly loaded assembly.
            foreach (IAssemblySymbol? assemblySymbol in config.AssemblySymbols)
            {
                if (assemblySymbol is null)
                    continue;

                using TextWriter textWriter = GetTextWriter(config.OutputPath, assemblySymbol.Name);
                IAssemblySymbolWriter writer = new CSharpFileBuilder(config.Logger,
                                                                     textWriter,
                                                                     config.Loader,
                                                                     config.SymbolFilter,
                                                                     config.AttributeDataSymbolFilter,
                                                                     config.Header,
                                                                     config.ExceptionMessage,
                                                                     config.IncludeAssemblyAttributes);
                writer.WriteAssembly(assemblySymbol);
            }

            if (config.Loader.HasRoslynDiagnostics(out IReadOnlyList<Diagnostic> roslynDiagnostics))
            {
                foreach (Diagnostic warning in roslynDiagnostics)
                {
                    config.Logger.LogWarning(warning.Id, warning.ToString());
                }
            }

            if (config.Loader.HasLoadWarnings(out IReadOnlyList<AssemblyLoadWarning> loadWarnings))
            {
                foreach (AssemblyLoadWarning warning in loadWarnings)
                {
                    config.Logger.LogWarning(warning.DiagnosticId, warning.Message);
                }
            }
        }

        // Creates a TextWriter capable of writing into Console or a cs file.
        private static TextWriter GetTextWriter(string? outputDirPath, string assemblyName)
        {
            if (outputDirPath is null)
            {
                return Console.Out;
            }

            string fileName = assemblyName + ".cs";
            if (Directory.Exists(outputDirPath))
            {
                return File.CreateText(Path.Combine(outputDirPath, fileName));
            }

            return File.CreateText(outputDirPath);
        }
    }
}
