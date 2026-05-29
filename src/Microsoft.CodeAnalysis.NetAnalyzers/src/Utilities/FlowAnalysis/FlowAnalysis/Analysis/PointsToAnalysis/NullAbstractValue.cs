// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis
{
    /// <summary>
    /// Abstract null value for <see cref="AnalysisEntity"/>/<see cref="IOperation"/> tracked by <see cref="PointsToAnalysis"/>.
    /// </summary>
    public enum NullAbstractValue
    {
        Invalid,
        Undefined,
        Null,
        NotNull,
        MaybeNull
    }
}
