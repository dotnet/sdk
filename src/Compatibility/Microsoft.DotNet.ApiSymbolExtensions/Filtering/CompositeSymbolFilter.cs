// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiSymbolExtensions.Filtering
{
    /// <summary>
    /// Implements the composite pattern, group the list of <see cref="ISymbol"/> and interact with them
    /// the same way as a single instance of a <see cref="ISymbol"/> object.
    /// </summary>
    public sealed class CompositeSymbolFilter(params IEnumerable<ISymbolFilter> filters) : ISymbolFilter
    {
        /// <summary>
        /// List on inner filters.
        /// </summary>
        public List<ISymbolFilter> Filters { get; } = [.. filters];

        /// <summary>
        /// Creates a composite filter to exclude APIs using the DocIDs provided in the specifed file paths.
        /// </summary>
        /// <param name="apiExclusionFilePaths">A collection of paths where the exclusion files should be searched.</param>
        /// <param name="respectInternals">Whether to include internal symbols or not.</param>
        /// <param name="includeEffectivelyPrivateSymbols">Whether to include effectively private symbols or not.</param>
        /// <param name="includeExplicitInterfaceImplementationSymbols">Whether to include explicit interface implementation symbols or not.</param>
        /// <returns>An instance of the symbol filter.</returns>
        public static ISymbolFilter GetSymbolFilterFromFiles(string[]? apiExclusionFilePaths,
                                                                     bool respectInternals = false,
                                                                     bool includeEffectivelyPrivateSymbols = true,
                                                                     bool includeExplicitInterfaceImplementationSymbols = true)
        {
            DocIdSymbolFilter? docIdSymbolFilter =
                apiExclusionFilePaths?.Count() > 0 ?
                DocIdSymbolFilter.CreateFromFiles(apiExclusionFilePaths) : null;

            return GetCompositeSymbolFilter(docIdSymbolFilter, respectInternals, includeEffectivelyPrivateSymbols, includeExplicitInterfaceImplementationSymbols, withImplicitSymbolFilter: true);
        }

        /// <summary>
        /// Creates a composite filter to exclude APIs using the DocIDs provided in the specifed list.
        /// </summary>
        /// <param name="apiExclusionList">A collection of exclusion list.</param>
        /// <param name="respectInternals">Whether to include internal symbols or not.</param>
        /// <param name="includeEffectivelyPrivateSymbols">Whether to include effectively private symbols or not.</param>
        /// <param name="includeExplicitInterfaceImplementationSymbols">Whether to include explicit interface implementation symbols or not.</param>
        /// <returns>An instance of the symbol filter.</returns>
        public static ISymbolFilter GetSymbolFilterFromList(string[]? apiExclusionList,
                                                                    bool respectInternals = false,
                                                                    bool includeEffectivelyPrivateSymbols = true,
                                                                    bool includeExplicitInterfaceImplementationSymbols = true)
        {
            DocIdSymbolFilter? docIdSymbolFilter =
                apiExclusionList?.Count() > 0 ?
                DocIdSymbolFilter.CreateFromDocIDs(apiExclusionList) : null;

            return GetCompositeSymbolFilter(docIdSymbolFilter, respectInternals, includeEffectivelyPrivateSymbols, includeExplicitInterfaceImplementationSymbols, withImplicitSymbolFilter: true);
        }

        /// <summary>
        /// Creates an composite filter to exclude attributes using the DocID provided in the specified file paths.
        /// </summary>
        /// <param name="attributeExclusionFilePaths">A collection of paths where the exclusion files should be searched.</param>
        /// <param name="respectInternals">Whether to include internal symbols or not.</param>
        /// <param name="includeEffectivelyPrivateSymbols">Whether to include effectively private symbols or not.</param>
        /// <param name="includeExplicitInterfaceImplementationSymbols">Whether to include explicit interface implementation symbols or not.</param>
        /// <returns>An instance of the attribute filter.</returns>
        public static ISymbolFilter GetAttributeFilterFromPaths(string[]? attributeExclusionFilePaths,
                                                                        bool respectInternals = false,
                                                                        bool includeEffectivelyPrivateSymbols = true,
                                                                        bool includeExplicitInterfaceImplementationSymbols = true)
        {
            DocIdSymbolFilter? docIdSymbolFilter =
                attributeExclusionFilePaths?.Count() > 0 ?
                DocIdSymbolFilter.CreateFromFiles(attributeExclusionFilePaths) : null;

            return GetCompositeSymbolFilter(docIdSymbolFilter, respectInternals, includeEffectivelyPrivateSymbols, includeExplicitInterfaceImplementationSymbols, withImplicitSymbolFilter: false);
        }

        /// <summary>
        /// Creates an composite filter to exclude attributes using the DocID provided in the specified list.
        /// </summary>
        /// <param name="attributeExclusionList">A collection of exclusion list.</param>
        /// <param name="respectInternals">Whether to include internal symbols or not.</param>
        /// <param name="includeEffectivelyPrivateSymbols">Whether to include effectively private symbols or not.</param>
        /// <param name="includeExplicitInterfaceImplementationSymbols">Whether to include explicit interface implementation symbols or not.</param>
        /// <returns>An instance of the attribute filter.</returns>
        public static ISymbolFilter GetAttributeFilterFromList(string[]? attributeExclusionList,
                                                                       bool respectInternals = false,
                                                                       bool includeEffectivelyPrivateSymbols = true,
                                                                       bool includeExplicitInterfaceImplementationSymbols = true)
        {
            DocIdSymbolFilter? docIdSymbolFilter =
                attributeExclusionList?.Count() > 0 ?
                DocIdSymbolFilter.CreateFromDocIDs(attributeExclusionList) : null;

            return GetCompositeSymbolFilter(docIdSymbolFilter, respectInternals, includeEffectivelyPrivateSymbols, includeExplicitInterfaceImplementationSymbols, withImplicitSymbolFilter: false);
        }

        private static ISymbolFilter GetCompositeSymbolFilter(DocIdSymbolFilter? customFilter,
                                                                      bool respectInternals,
                                                                      bool includeEffectivelyPrivateSymbols,
                                                                      bool includeExplicitInterfaceImplementationSymbols,
                                                                      bool withImplicitSymbolFilter)
        {
            AccessibilitySymbolFilter accessibilitySymbolFilter = new(
                    respectInternals,
                    includeEffectivelyPrivateSymbols,
                    includeExplicitInterfaceImplementationSymbols);

            CompositeSymbolFilter filter = new();

            if (customFilter != null)
            {
                filter.Add(customFilter);
            }
            if (withImplicitSymbolFilter)
            {
                filter.Add(new ImplicitSymbolFilter());
            }

            filter.Add(accessibilitySymbolFilter);

            return filter;
        }

        /// <summary>
        /// Determines whether the <see cref="ISymbol"/> should be included.
        /// </summary>
        /// <param name="symbol"><see cref="ISymbol"/> to evaluate.</param>
        /// <returns>True to include the <paramref name="symbol"/> or false to filter it out.</returns>
        public bool Include(ISymbol symbol) => Filters.All(f => f.Include(symbol));

        /// <summary>
        /// Add a filter object to a list of filters.
        /// </summary>
        /// <param name="filter">The <see cref="ISymbolFilter" /> to include to the list of filters.</param>
        /// <returns>Returns the current instance of the class.</returns>
        public CompositeSymbolFilter Add(ISymbolFilter filter)
        {
            Filters.Add(filter);
            return this;
        }
    }
}
