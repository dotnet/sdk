// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis
{
    /// <summary>
    /// The entity that presents either a symbol and its an creation operation (if possible).
    /// </summary>
    internal class DeferredTypeEntity : CacheBasedEquatable<DeferredTypeEntity>, IDeferredTypeEntity
    {
        public ISymbol Symbol { get; }

        public IOperation? CreationOperation { get; }

        public DeferredTypeEntity(ISymbol symbol, IOperation? creationOperation)
        {
            Symbol = symbol;
            CreationOperation = creationOperation;
        }

        protected override void ComputeHashCodeParts(ref RoslynHashCode hashCode)
        {
            hashCode.Add(Symbol.GetHashCode());
            hashCode.Add(CreationOperation.GetHashCodeOrDefault());
        }

        protected override bool ComputeEqualsByHashCodeParts(CacheBasedEquatable<DeferredTypeEntity> obj)
        {
            var other = (DeferredTypeEntity)obj;
            return other.Symbol.GetHashCode() == Symbol.GetHashCode()
                   && other.CreationOperation.GetHashCodeOrDefault() == CreationOperation.GetHashCodeOrDefault();
        }
    }
}
