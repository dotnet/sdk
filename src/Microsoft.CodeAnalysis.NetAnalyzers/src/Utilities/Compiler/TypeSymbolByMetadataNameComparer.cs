// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities
{
    /// <summary>
    /// <see cref="IComparer{T}"/> for <see cref="ITypeSymbol"/>s sorted by display strings.
    /// </summary>
    internal class TypeSymbolByMetadataNameComparer : IComparer<ITypeSymbol>
    {
        /// <summary>
        /// Constructs.
        /// </summary>
        public TypeSymbolByMetadataNameComparer(Compilation compilation)
            : this(SymbolDisplayStringCache.GetOrCreate(compilation))
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="symbolDisplayStringCache"></param>
        public TypeSymbolByMetadataNameComparer(SymbolDisplayStringCache symbolDisplayStringCache)
        {
            this.SymbolDisplayStringCache = symbolDisplayStringCache ?? throw new ArgumentNullException(nameof(symbolDisplayStringCache));
        }

        public SymbolDisplayStringCache SymbolDisplayStringCache { get; }

        public int Compare(ITypeSymbol x, ITypeSymbol y)
        {
            return StringComparer.Ordinal.Compare(this.SymbolDisplayStringCache[x], y.ToDisplayString());
        }
    }
}
