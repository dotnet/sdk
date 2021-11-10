using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis
{
    internal partial class EnumerationEntity
    {
        /// <summary>
        /// The entity that creates a local or paramter deferred type.
        /// </summary>
        private class DeferredTypeEntity : CacheBasedEquatable<DeferredTypeEntity>
        {
            public ISymbol? Symbol { get; }

            public IOperation? CreationOperation { get; }

            public DeferredTypeEntity(ISymbol? symbol, IOperation? creationOperation)
            {
                Symbol = symbol;
                CreationOperation = creationOperation;
            }

            protected override void ComputeHashCodeParts(ref RoslynHashCode hashCode)
            {
                hashCode.Add(Symbol.GetHashCodeOrDefault());
                hashCode.Add(CreationOperation.GetHashCodeOrDefault());
            }

            protected override bool ComputeEqualsByHashCodeParts(CacheBasedEquatable<DeferredTypeEntity> obj)
            {
                var other = (DeferredTypeEntity)obj;
                return other.Symbol.GetHashCodeOrDefault() == Symbol.GetHashCodeOrDefault()
                       && other.CreationOperation.GetHashCodeOrDefault() == CreationOperation.GetHashCodeOrDefault();
            }
        }
    }
}
