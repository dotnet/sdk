// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        // A curated set of internal compiler attributes that, when defined in an assembly, enable out-of-band
        // language features. GenAPI emits these by default so that the generated reference assembly keeps working
        // with the same language features as the original assembly.
        // NOTE: This is a heuristic curated list. A more general, namespace-based approach is tracked by
        // https://github.com/dotnet/sdk/issues/54527.
        private static readonly string[] s_compilerAttributes =
        [
            "T:System.Runtime.CompilerServices.IsExternalInit",
            "T:System.Runtime.CompilerServices.InterpolatedStringHandlerAttribute",
            "T:System.Runtime.CompilerServices.InterpolatedStringHandlerArgumentAttribute",
            "T:System.Runtime.CompilerServices.RequiredMemberAttribute",
            "T:System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute",
            "T:System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute",
            "T:System.Runtime.CompilerServices.CollectionBuilderAttribute",
            "T:System.Diagnostics.CodeAnalysis.AllowNullAttribute",
            "T:System.Diagnostics.CodeAnalysis.DisallowNullAttribute",
            "T:System.Diagnostics.CodeAnalysis.MaybeNullAttribute",
            "T:System.Diagnostics.CodeAnalysis.NotNullAttribute",
            "T:System.Diagnostics.CodeAnalysis.MaybeNullWhenAttribute",
            "T:System.Diagnostics.CodeAnalysis.NotNullWhenAttribute",
            "T:System.Diagnostics.CodeAnalysis.NotNullIfNotNullAttribute",
            "T:System.Diagnostics.CodeAnalysis.DoesNotReturnAttribute",
            "T:System.Diagnostics.CodeAnalysis.DoesNotReturnIfAttribute",
            "T:System.Diagnostics.CodeAnalysis.MemberNotNullAttribute",
            "T:System.Diagnostics.CodeAnalysis.MemberNotNullWhenAttribute"
        ];

        /// <summary>
        /// Initialize and run Roslyn-based GenAPI tool using <see cref="GenAPIOptions"/>.
        /// </summary>
        public static void Run(ILog log, GenAPIOptions options)
        {
            (IAssemblySymbolLoader loader, Dictionary<string, IAssemblySymbol> assemblySymbols) = AssemblySymbolLoader.CreateFromFiles(
                log,
                options.AssembliesPaths,
                options.AssemblyReferencesPaths,
                assembliesToExclude: [],
                respectInternals: options.RespectInternals);

            Run(log,
                loader,
                assemblySymbols,
                options.OutputPath,
                options.HeaderFile,
                options.ExceptionMessage,
                options.ExcludeApiFiles,
                options.ExcludeAttributesFiles,
                options.IncludeApiFiles,
                options.ExcludeInternalCompilerAttributes,
                options.RespectInternals,
                options.IncludeAssemblyAttributes);
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
            string[]? includeApiFiles,
            bool excludeInternalCompilerAttributes,
            bool respectInternals,
            bool includeAssemblyAttributes)
        {
            // Shared accessibility filter for the API and Attribute composite filters.
            AccessibilitySymbolFilter accessibilitySymbolFilter = new(
                respectInternals,
                includeEffectivelyPrivateSymbols: true,
                includeExplicitInterfaceImplementationSymbols: true);

            // Build an optional filter that includes additional APIs which would otherwise be filtered out by the
            // accessibility filter: APIs explicitly listed via includeApiFiles and, unless opted out, the curated set
            // of internal compiler attributes.
            ISymbolFilter? additionalApiInclusionFilter = CreateAdditionalApiInclusionFilter(includeApiFiles, excludeInternalCompilerAttributes);

            // Invoke the CSharpFileBuilder for each directly loaded assembly.
            foreach (KeyValuePair<string, IAssemblySymbol> kvp in assemblySymbols)
            {
                using TextWriter textWriter = GetTextWriter(outputPath, kvp.Key);

                ISymbolFilter symbolFilter = SymbolFilterFactory.GetFilterFromFiles(
                        excludeApiFiles, accessibilitySymbolFilter,
                        respectInternals: respectInternals,
                        additionalApiInclusionFilter: additionalApiInclusionFilter);
                ISymbolFilter attributeDataSymbolFilter = SymbolFilterFactory.GetFilterFromFiles(
                        excludeAttributesFiles, accessibilitySymbolFilter,
                        respectInternals: respectInternals,
                        additionalApiInclusionFilter: additionalApiInclusionFilter);

                string? headerText = headerFile != null ? File.ReadAllText(headerFile) : null;

                CSharpFileBuilder fileBuilder = new(log,
                    textWriter,
                    loader,
                    symbolFilter,
                    attributeDataSymbolFilter,
                    headerText,
                    exceptionMessage,
                    includeAssemblyAttributes,
                    loader.MetadataReferences,
                    addPartialModifier: true);

                fileBuilder.WriteAssembly(kvp.Value);
            }
        }

        // Builds an OR composite filter that includes APIs listed in includeApiFiles and, unless opted out, the
        // curated set of internal compiler attributes. Returns null when there is nothing additional to include.
        private static ISymbolFilter? CreateAdditionalApiInclusionFilter(string[]? includeApiFiles, bool excludeInternalCompilerAttributes)
        {
            bool hasIncludeApiFiles = includeApiFiles?.Length > 0;
            if (!hasIncludeApiFiles && excludeInternalCompilerAttributes)
            {
                return null;
            }

            CompositeSymbolFilter inclusionFilter = new(CompositeSymbolFilterMode.Or);

            if (hasIncludeApiFiles)
            {
                inclusionFilter.Add(DocIdSymbolFilter.CreateFromFiles(includeApiFiles!, includeDocIds: true));
            }

            if (!excludeInternalCompilerAttributes)
            {
                inclusionFilter.Add(DocIdSymbolFilter.CreateFromLists(s_compilerAttributes, includeDocIds: true));
            }

            return inclusionFilter;
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
