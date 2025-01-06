// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.GenAPI.Filtering;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;

namespace Microsoft.DotNet.GenAPI;

/// <summary>
/// Factory class that creates composite filters based on the given exclusion lists.
/// </summary>
public static class SymbolFilterFactory
{
    /// <summary>
    /// Creates a symbol filter based on the given exclusion file paths.
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
    /// Creates a symbol filter based on the given exclusion list.
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
    /// Creates an attribute filter based on the given exclusion file paths.
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
    /// Creates an attribute filter based on the given exclusion list.
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
}
