// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiSymbolExtensions.Filtering
{
    /// <summary>
    /// Implements the composite pattern, group the list of <see cref="ISymbol"/> and interact with them
    /// the same way as a single instance of a <see cref="ISymbol"/> object.
    /// </summary>
    public sealed class CompositeSymbolFilter(CompositeSymbolFilterMode mode = CompositeSymbolFilterMode.And,
        params ISymbolFilter[] symbolFilters) : ISymbolFilter
    {
        private readonly List<ISymbolFilter> _filters = new(symbolFilters);

        /// <summary>
        /// Behavior of combination.  Default is `And` which requires all filters to include the symbol.
        /// `Or` will include the symbol if any filter includes the symbol.
        /// </summary>
        public CompositeSymbolFilterMode Mode { get; } = mode;

        /// <summary>
        /// List on inner filters.
        /// </summary>
        public IReadOnlyList<ISymbolFilter> Filters => _filters.AsReadOnly();

        /// <summary>
        /// Determines whether the <see cref="ISymbol"/> should be included.
        /// </summary>
        /// <param name="symbol"><see cref="ISymbol"/> to evaluate.</param>
        /// <returns>True to include the <paramref name="symbol"/> or false to filter it out.</returns>
        public bool Include(ISymbol symbol) => Mode switch
        {
            CompositeSymbolFilterMode.And => Filters.All(f => f.Include(symbol)),
            CompositeSymbolFilterMode.Or => Filters.Any(f => f.Include(symbol)),
            _ => throw new NotImplementedException()
        };

        /// <summary>
        /// Add a filter object to a list of filters.
        /// </summary>
        /// <param name="filter">The <see cref="ISymbolFilter" /> to include to the list of filters.</param>
        /// <returns>Returns the current instance of the class.</returns>
        public CompositeSymbolFilter Add(ISymbolFilter filter)
        {
            _filters.Add(filter);
            return this;
        }
    }

    /// <summary>
    /// Mode of combination of symbol filters
    /// </summary>
    public enum CompositeSymbolFilterMode
    {
        /// <summary>
        /// All filters must be true for a symbol to be included.
        /// </summary>
        And,

        /// <summary>
        /// Any filter can be true for a symbol to be included.
        /// </summary>
        Or
    }
}
