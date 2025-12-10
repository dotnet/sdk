// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
