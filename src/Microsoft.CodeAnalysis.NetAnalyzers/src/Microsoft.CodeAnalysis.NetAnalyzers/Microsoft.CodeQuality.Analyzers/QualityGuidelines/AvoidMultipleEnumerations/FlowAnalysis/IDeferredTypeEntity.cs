// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis
{
    /// <summary>
    /// The entity represents the source of a deferred type value. It might be
    /// 1. A local symbol or parameter symbol.
    /// 2. An operation returns deferred type.
    /// 3. A set of <see cref="AbstractLocation"/>, represent all the possible value source locations.
    /// </summary>
#pragma warning disable CA1040 // Avoid empty interfaces - https://github.com/dotnet/roslyn-analyzers/issues/6379
    internal interface IDeferredTypeEntity
#pragma warning restore CA1040 // Avoid empty interfaces
    {
    }
}
