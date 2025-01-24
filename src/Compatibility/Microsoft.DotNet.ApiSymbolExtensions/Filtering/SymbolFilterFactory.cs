// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiSymbolExtensions.Filtering;

public static class SymbolFilterFactory
{
    /// <summary>
    /// Creates a composite filter to exclude APIs using the DocIDs provided in the specifed file paths.
    /// </summary>
    /// <param name="apiExclusionFilePaths">A collection of paths where the exclusion files should be searched.</param>
    /// <param name="respectInternals">Whether to include internal symbols or not.</param>
    /// <param name="includeEffectivelyPrivateSymbols">Whether to include effectively private symbols or not.</param>
    /// <param name="includeExplicitInterfaceImplementationSymbols">Whether to include explicit interface implementation symbols or not.</param>
    /// <returns>An instance of the symbol filter.</returns>
    public static ISymbolFilter GetFilterFromFiles(string[]? apiExclusionFilePaths,
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
    public static ISymbolFilter GetFilterFromList(string[]? apiExclusionList,
                                                                bool respectInternals = false,
                                                                bool includeEffectivelyPrivateSymbols = true,
                                                                bool includeExplicitInterfaceImplementationSymbols = true)
    {
        DocIdSymbolFilter? docIdSymbolFilter =
            apiExclusionList?.Count() > 0 ?
            DocIdSymbolFilter.CreateFromLists(apiExclusionList) : null;

        return GetCompositeSymbolFilter(docIdSymbolFilter, respectInternals, includeEffectivelyPrivateSymbols, includeExplicitInterfaceImplementationSymbols, withImplicitSymbolFilter: true);
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
}
