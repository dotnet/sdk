// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.GenAPI.Filtering;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;

namespace Microsoft.DotNet.GenAPI;

public static class SymbolFilterFactory
{
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
