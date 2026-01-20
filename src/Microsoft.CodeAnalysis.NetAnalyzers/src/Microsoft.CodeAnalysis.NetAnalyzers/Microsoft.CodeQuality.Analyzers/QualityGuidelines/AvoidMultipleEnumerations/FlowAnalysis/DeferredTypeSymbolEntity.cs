// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
