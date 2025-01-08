// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET
using System.Text.RegularExpressions;
#endif
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;

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
        public static void Run(ILog logger,
            IAssemblySymbolLoader loader,
            Dictionary<string, IAssemblySymbol> assemblySymbols,
            string? outputPath,
            string? headerFile,
            string? exceptionMessage,
            string[]? excludeApiFiles,
            string[]? excludeAttributesFiles,
            bool respectInternals,
            bool includeAssemblyAttributes)
        {

            // Invoke an assembly symbol writer for each directly loaded assembly.
            foreach (KeyValuePair<string, IAssemblySymbol> kvp in assemblySymbols)
            {
                using TextWriter textWriter = GetTextWriter(outputPath, kvp.Key);
                CSharpFileBuilder writer = new(logger,
                                               textWriter,
                                               loader,
                                               CompositeSymbolFilter.GetSymbolFilterFromFiles(excludeApiFiles, respectInternals),
                                               CompositeSymbolFilter.GetAttributeFilterFromPaths(excludeAttributesFiles, respectInternals),
                                               headerFile,
                                               exceptionMessage,
                                               includeAssemblyAttributes);
                writer.WriteAssembly(kvp.Value);
            }

            loader.LogAllDiagnostics();
            loader.LogAllWarnings();
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
