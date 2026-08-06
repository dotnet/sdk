// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiSymbolExtensions.Filtering;

/// <summary>
/// A factory class to create symbol filters.
/// </summary>
public static class SymbolFilterFactory
{
    /// <summary>
    /// Creates a composite filter to exclude APIs using the DocIDs provided in the specifed file paths.
    /// </summary>
    /// <param name="apiExclusionFilePaths">A collection of paths where the exclusion files should be searched.</param>
    /// <param name="accessibilitySymbolFilter">An optional custom accessibility symbol filter to use.</param>
    /// <param name="respectInternals">Whether to include internal symbols or not.</param>
    /// <param name="includeEffectivelyPrivateSymbols">Whether to include effectively private symbols or not.</param>
    /// <param name="includeExplicitInterfaceImplementationSymbols">Whether to include explicit interface implementation symbols or not.</param>
    /// <param name="includeImplicitSymbolFilter">Whether to include implicit symbols or not.</param>
    /// <returns>An instance of the symbol filter.</returns>
    public static ISymbolFilter GetFilterFromFiles(string[]? apiExclusionFilePaths,
                                                   AccessibilitySymbolFilter? accessibilitySymbolFilter = null,
                                                   bool respectInternals = false,
                                                   bool includeEffectivelyPrivateSymbols = true,
                                                   bool includeExplicitInterfaceImplementationSymbols = true,
                                                   bool includeImplicitSymbolFilter = true)
    {
        DocIdSymbolFilter? docIdSymbolFilter =
            apiExclusionFilePaths?.Length > 0 ?
            DocIdSymbolFilter.CreateFromFiles(apiExclusionFilePaths) : null;

        return GetCompositeSymbolFilter(docIdSymbolFilter, accessibilitySymbolFilter, respectInternals, includeEffectivelyPrivateSymbols, includeExplicitInterfaceImplementationSymbols, includeImplicitSymbolFilter);
    }

    /// <summary>
    /// Creates a composite filter to exclude APIs using the DocIDs provided in the specifed list.
    /// </summary>
    /// <param name="apiExclusionList">A collection of exclusion list.</param>
    /// <param name="accessibilitySymbolFilter">An optional custom accessibility symbol filter to use.</param>
    /// <param name="respectInternals">Whether to include internal symbols or not.</param>
    /// <param name="includeEffectivelyPrivateSymbols">Whether to include effectively private symbols or not.</param>
    /// <param name="includeExplicitInterfaceImplementationSymbols">Whether to include explicit interface implementation symbols or not.</param>
    /// <param name="includeImplicitSymbolFilter">Whether to include implicit symbols or not.</param>
    /// <returns>An instance of the symbol filter.</returns>
    public static ISymbolFilter GetFilterFromList(string[]? apiExclusionList,
                                                  AccessibilitySymbolFilter? accessibilitySymbolFilter = null,
                                                  bool respectInternals = false,
                                                  bool includeEffectivelyPrivateSymbols = true,
                                                  bool includeExplicitInterfaceImplementationSymbols = true,
                                                  bool includeImplicitSymbolFilter = true)
    {
        DocIdSymbolFilter? docIdSymbolFilter =
            apiExclusionList?.Count() > 0 ?
            DocIdSymbolFilter.CreateFromLists(apiExclusionList) : null;

        return GetCompositeSymbolFilter(docIdSymbolFilter, accessibilitySymbolFilter, respectInternals, includeEffectivelyPrivateSymbols, includeExplicitInterfaceImplementationSymbols, includeImplicitSymbolFilter);
    }

    private static ISymbolFilter GetCompositeSymbolFilter(DocIdSymbolFilter? customFilter,
                                                          AccessibilitySymbolFilter? accessibilitySymbolFilter,
                                                          bool respectInternals,
                                                          bool includeEffectivelyPrivateSymbols,
                                                          bool includeExplicitInterfaceImplementationSymbols,
                                                          bool includeImplicitSymbolFilter)
    {
        accessibilitySymbolFilter ??= new(
                respectInternals,
                includeEffectivelyPrivateSymbols,
                includeExplicitInterfaceImplementationSymbols);

        CompositeSymbolFilter filter = new();

        if (customFilter != null)
        {
            filter.Add(customFilter);
        }
        if (includeImplicitSymbolFilter)
        {
            filter.Add(new ImplicitSymbolFilter());
        }

        filter.Add(accessibilitySymbolFilter);

        return filter;
    }
}
