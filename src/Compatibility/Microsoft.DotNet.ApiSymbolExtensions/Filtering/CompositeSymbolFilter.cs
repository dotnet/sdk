// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiSymbolExtensions.Filtering
{
    /// <summary>
    /// Implements the composite pattern, group the list of <see cref="ISymbol"/> and interact with them
    /// the same way as a single instance of a <see cref="ISymbol"/> object.
    /// </summary>
    /// <param name="mode">Determines how the inner filters are combined. Defaults to <see cref="CompositeSymbolFilterMode.And"/>.</param>
    /// <param name="filters">The inner filters to combine.</param>
    public sealed class CompositeSymbolFilter(CompositeSymbolFilterMode mode = CompositeSymbolFilterMode.And,
        params IEnumerable<ISymbolFilter> filters) : ISymbolFilter
    {
        /// <summary>
        /// Determines how the inner filters are combined. <see cref="CompositeSymbolFilterMode.And"/> requires all
        /// filters to include a symbol, while <see cref="CompositeSymbolFilterMode.Or"/> includes a symbol if any
        /// filter includes it.
        /// </summary>
        public CompositeSymbolFilterMode Mode { get; } = mode;

        /// <summary>
        /// List on inner filters.
        /// </summary>
        public List<ISymbolFilter> Filters { get; } = new(filters);

        /// <summary>
        /// Determines whether the <see cref="ISymbol"/> should be included.
        /// </summary>
        /// <param name="symbol"><see cref="ISymbol"/> to evaluate.</param>
        /// <returns>True to include the <paramref name="symbol"/> or false to filter it out.</returns>
        public bool Include(ISymbol symbol) => Mode switch
        {
            CompositeSymbolFilterMode.And => Filters.All(f => f.Include(symbol)),
            CompositeSymbolFilterMode.Or => Filters.Any(f => f.Include(symbol)),
            _ => throw new InvalidOperationException()
        };

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

    /// <summary>
    /// Determines how the inner filters of a <see cref="CompositeSymbolFilter"/> are combined.
    /// </summary>
    public enum CompositeSymbolFilterMode
    {
        /// <summary>
        /// All filters must include a symbol for it to be included.
        /// </summary>
        And,

        /// <summary>
        /// Any filter can include a symbol for it to be included.
        /// </summary>
        Or
    }
}
