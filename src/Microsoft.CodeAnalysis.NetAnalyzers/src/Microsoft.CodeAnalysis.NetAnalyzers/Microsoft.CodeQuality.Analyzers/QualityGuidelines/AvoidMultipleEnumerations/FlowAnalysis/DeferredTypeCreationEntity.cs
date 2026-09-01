// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis
{
    /// <summary>
    /// A deferred type entity created by an operation.
    /// </summary>
    internal class DeferredTypeCreationEntity : CacheBasedEquatable<DeferredTypeCreationEntity>, IDeferredTypeEntity
    {
        public IOperation CreationOperation { get; }

        public DeferredTypeCreationEntity(IOperation creationOperation)
            => CreationOperation = creationOperation;

        protected override bool ComputeEqualsByHashCodeParts(CacheBasedEquatable<DeferredTypeCreationEntity> obj)
        {
            var other = (DeferredTypeCreationEntity)obj;
            return other.CreationOperation.GetHashCode() == CreationOperation.GetHashCode();
        }

        protected override void ComputeHashCodeParts(ref RoslynHashCode hashCode)
            => hashCode.Add(CreationOperation.GetHashCode());
    }
}
