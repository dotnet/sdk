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
        /// Initialize and run Roslyn-based GenAPI tool specifying the assemblies to load.
        /// </summary>
        public static void Run(ILog log,
            string[] assembliesPaths,
            string[]? assemblyReferencesPaths,
            string? outputPath,
            string? headerFile,
            string? exceptionMessage,
            string[]? excludeApiFiles,
            string[]? excludeAttributesFiles,
            bool respectInternals,
            bool includeAssemblyAttributes)
        {
            (IAssemblySymbolLoader loader, Dictionary<string, IAssemblySymbol> assemblySymbols) = AssemblySymbolLoader.CreateFromFiles(
                log,
                assembliesPaths,
                assemblyReferencesPaths,
                respectInternals: respectInternals);

            Run(log,
                loader,
                assemblySymbols,
                outputPath,
                headerFile,
                exceptionMessage,
                excludeApiFiles,
                excludeAttributesFiles,
                respectInternals,
                includeAssemblyAttributes);
        }

        /// <summary>
        /// Initialize and run Roslyn-based GenAPI tool using an assembly symbol loader that pre-loaded the assemblies separately.
        /// </summary>
        public static void Run(ILog log,
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
            // Shared accessibility filter for the API and Attribute composite filters.
            AccessibilitySymbolFilter accessibilitySymbolFilter = new(
                respectInternals,
                includeEffectivelyPrivateSymbols: true,
                includeExplicitInterfaceImplementationSymbols: true);

            // Invoke the CSharpFileBuilder for each directly loaded assembly.
            foreach (KeyValuePair<string, IAssemblySymbol> kvp in assemblySymbols)
            {
                using TextWriter textWriter = GetTextWriter(outputPath, kvp.Key);

                ISymbolFilter symbolFilter = SymbolFilterFactory.GetFilterFromFiles(
                        excludeApiFiles, accessibilitySymbolFilter,
                        respectInternals: respectInternals);
                ISymbolFilter attributeDataSymbolFilter = SymbolFilterFactory.GetFilterFromFiles(
                        excludeAttributesFiles, accessibilitySymbolFilter,
                        respectInternals: respectInternals);

                CSharpFileBuilder fileBuilder = new(log,
                    textWriter,
                    loader,
                    symbolFilter,
                    attributeDataSymbolFilter,
                    headerFile,
                    exceptionMessage,
                    includeAssemblyAttributes,
                    loader.MetadataReferences,
                    addPartialModifier: true);

                fileBuilder.WriteAssembly(kvp.Value);
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
