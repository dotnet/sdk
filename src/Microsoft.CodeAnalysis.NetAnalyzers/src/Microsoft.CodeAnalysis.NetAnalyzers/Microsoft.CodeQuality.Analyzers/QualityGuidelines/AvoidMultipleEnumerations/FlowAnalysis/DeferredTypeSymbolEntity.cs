// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis
{
    /// <summary>
    /// Represents a symbol with the deferred type.
    /// </summary>
    internal class DeferredTypeSymbolEntity : CacheBasedEquatable<DeferredTypeSymbolEntity>, IDeferredTypeEntity
    {
        public ISymbol Symbol { get; }

        public DeferredTypeSymbolEntity(ISymbol symbol) => Symbol = symbol;

        protected override void ComputeHashCodeParts(ref RoslynHashCode hashCode) => hashCode.Add(Symbol.GetHashCode());

        protected override bool ComputeEqualsByHashCodeParts(CacheBasedEquatable<DeferredTypeSymbolEntity> obj)
        {
            var other = (DeferredTypeSymbolEntity)obj;
            return other.Symbol.GetHashCode() == Symbol.GetHashCode();
        }
    }
}
