// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if HAS_IOPERATION

namespace Analyzer.Utilities.Lightup
{
    using Microsoft.CodeAnalysis;

    internal interface IOperationWrapper
    {
        IOperation? WrappedOperation { get; }
    }
}

#endif
