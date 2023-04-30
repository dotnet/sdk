// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiSymbolExtensions.Filtering
{
    /// <summary>
    /// Implements the composite pattern, group the list of <see cref="ISymbol"/> and interact with them
    /// the same way as a single instance of a <see cref="ISymbol"/> object.
    /// </summary>
    public sealed class CompositeSymbolFilter : ISymbolFilter
    {
        /// <summary>
        /// List on inner filters.
        /// </summary>
        public List<ISymbolFilter> Filters { get; } = new();

        /// <summary>
        /// Determines whether the <see cref="ISymbol"/> should be included.
        /// </summary>
        /// <param name="symbol"><see cref="ISymbol"/> to evaluate.</param>
        /// <returns>True to include the <paramref name="symbol"/> or false to filter it out.</returns>
        public bool Include(ISymbol symbol) => Filters.All(f => f.Include(symbol));

        /// <summary>
        /// Add a filter object to a list of filters.
        /// </summary>
        /// <typeparam name="T"><see cref="ISymbolFilter" /> to evaluate.</typeparam>
        /// <returns>Returns the current instance of the class.</returns>
        public CompositeSymbolFilter Add(ISymbolFilter filter)
        {
            Filters.Add(filter);
            return this;
        }
    }
}
