// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiSymbolExtensions.Filtering
{
    /// <summary>
    /// Implements the logic of filtering out symbols based on a provided accessibility level (i.e. public / internal).
    /// </summary>
    public class AccessibilitySymbolFilter(bool includeInternalSymbols,
        bool includeEffectivelyPrivateSymbols = false,
        bool includeExplicitInterfaceImplementationSymbols = false) : ISymbolFilter
    {
        /// <summary>
        /// Include internal API.
        /// </summary>
        public bool IncludeInternalSymbols { get; } = includeInternalSymbols;

        /// <summary>
        /// Include effectively private API.
        /// </summary>
        public bool IncludeEffectivelyPrivateSymbols { get; } = includeEffectivelyPrivateSymbols;

        /// <summary>
        /// Include explicit interface implementation API.
        /// </summary>
        public bool IncludeExplicitInterfaceImplementationSymbols { get; } = includeExplicitInterfaceImplementationSymbols;

        /// <inheritdoc />
        public bool Include(ISymbol symbol) =>
            symbol.IsVisibleOutsideOfAssembly(IncludeInternalSymbols,
                IncludeEffectivelyPrivateSymbols,
                IncludeExplicitInterfaceImplementationSymbols);
    }
}
