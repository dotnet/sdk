// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET
using System.Text.RegularExpressions;
#endif
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;
using Microsoft.DotNet.GenAPI.Filtering;

namespace Microsoft.DotNet.GenAPI
{
    /// <summary>
    /// Class to standardize initialization and running of GenAPI tool.
    /// Shared between CLI and MSBuild tasks frontends.
    /// </summary>
    public static class GenAPIApp
    {
        // Attributes that can work with local definition and no runtime support to light up
        // public API behavior in the language / compiler that are not defined in all supported frameworks
        // see https://github.com/dotnet/roslyn/blob/859f94ef2d8bf88527217bc9ad7661b6fbdf33a9/src/Compilers/Core/Portable/Symbols/Attributes/AttributeDescription.cs#L343
        private static readonly string[] s_compilerAttributes =
        [
            "T:System.Runtime.CompilerServices.IsExternalInit",
            "T:System.Runtime.CompilerServices.NullableAttribute",
            "T:System.Runtime.CompilerServices.NullableContextAttribute",
            "T:System.Runtime.CompilerServices.NullablePublicOnlyAttribute",
            "T:System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute",
            "T:System.Runtime.CompilerServices.InterpolatedStringHandlerAttribute",
            "T:System.Runtime.CompilerServices.InterpolatedStringHandlerArgumentAttribute",
            "T:System.Runtime.CompilerServices.RequiredMemberAttribute",
            "T:System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute",
            "T:System.Runtime.CompilerServices.CollectionBuilderAttribute"
        ];

        /// <summary>
        /// Initialize and run Roslyn-based GenAPI tool.
        /// </summary>
        public static void Run(ILog logger,
            string[] assemblies,
            string[]? assemblyReferences,
            string? outputPath,
            string? headerFile,
            string? exceptionMessage,
            string[]? excludeApiFiles,
            string[]? excludeAttributesFiles,
            bool excludeInternalCompilerAttributes,
            string[]? includeApiFiles,
            bool respectInternals,
            bool includeAssemblyAttributes)
        {
            bool resolveAssemblyReferences = assemblyReferences?.Length > 0;

            // Create, configure and execute the assembly loader.
            AssemblySymbolLoader loader = new(resolveAssemblyReferences, respectInternals);
            if (assemblyReferences is not null)
            {
                loader.AddReferenceSearchPaths(assemblyReferences);
            }
            IReadOnlyList<IAssemblySymbol?> assemblySymbols = loader.LoadAssemblies(assemblies);

            string headerFileText = ReadHeaderFile(headerFile);

            ISymbolFilter typeFilter = new AccessibilitySymbolFilter(respectInternals,
                includeEffectivelyPrivateSymbols: true,
                includeExplicitInterfaceImplementationSymbols: true);

            if (includeApiFiles is not null || !excludeInternalCompilerAttributes)
            {
                CompositeSymbolFilter compositeTypeFilter = new(mode: CompositeSymbolFilterMode.Or, typeFilter);

                if (includeApiFiles is not null)
                {
                    DocIdSymbolFilter includeApiFilesFilter = DocIdSymbolFilter.CreateFromFiles(includeApiFiles, includeDocIds: true);
                    compositeTypeFilter.Add(includeApiFilesFilter);
                }

                if (!excludeInternalCompilerAttributes)
                {
                    DocIdSymbolFilter excludeInternalCompilerAttributesFilter = new(s_compilerAttributes, includeDocIds: true);
                    compositeTypeFilter.Add(excludeInternalCompilerAttributesFilter);
                }

                typeFilter = compositeTypeFilter;
            }

            // Configure the symbol filter
            CompositeSymbolFilter symbolFilter = new();
            if (excludeApiFiles is not null)
            {
                symbolFilter.Add(DocIdSymbolFilter.CreateFromFiles(excludeApiFiles));
            }
            symbolFilter.Add(new ImplicitSymbolFilter());
            symbolFilter.Add(typeFilter);

            // Configure the attribute data symbol filter
            CompositeSymbolFilter attributeDataSymbolFilter = new();
            if (excludeAttributesFiles is not null)
            {
                attributeDataSymbolFilter.Add(DocIdSymbolFilter.CreateFromFiles(excludeAttributesFiles));
            }
            attributeDataSymbolFilter.Add(typeFilter);

            // Invoke the CSharpFileBuilder for each directly loaded assembly.
            foreach (IAssemblySymbol? assemblySymbol in assemblySymbols)
            {
                if (assemblySymbol is null)
                    continue;

                using TextWriter textWriter = GetTextWriter(outputPath, assemblySymbol.Name);
                textWriter.Write(headerFileText);

                using CSharpFileBuilder fileBuilder = new(logger,
                    symbolFilter,
                    attributeDataSymbolFilter,
                    textWriter,
                    exceptionMessage,
                    includeAssemblyAttributes,
                    loader.MetadataReferences);

                fileBuilder.WriteAssembly(assemblySymbol);
            }

            if (loader.HasRoslynDiagnostics(out IReadOnlyList<Diagnostic> roslynDiagnostics))
            {
                foreach (Diagnostic warning in roslynDiagnostics)
                {
                    logger.LogWarning(warning.Id, warning.ToString());
                }
            }

            if (loader.HasLoadWarnings(out IReadOnlyList<AssemblyLoadWarning> loadWarnings))
            {
                foreach (AssemblyLoadWarning warning in loadWarnings)
                {
                    logger.LogWarning(warning.DiagnosticId, warning.Message);
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

        // Read the header file if specified, or use default one.
        private static string ReadHeaderFile(string? headerFile)
        {
            const string defaultFileHeader = """
            //------------------------------------------------------------------------------
            // <auto-generated>
            //     This code was generated by a tool.
            //
            //     Changes to this file may cause incorrect behavior and will be lost if
            //     the code is regenerated.
            // </auto-generated>
            //------------------------------------------------------------------------------

            """;

            string header = !string.IsNullOrEmpty(headerFile) ?
                File.ReadAllText(headerFile) :
                defaultFileHeader;

#if NET
            header = header.ReplaceLineEndings();
#else
            header = Regex.Replace(header, @"\r\n|\n\r|\n|\r", Environment.NewLine);
#endif

            return header;
        }
    }
}
