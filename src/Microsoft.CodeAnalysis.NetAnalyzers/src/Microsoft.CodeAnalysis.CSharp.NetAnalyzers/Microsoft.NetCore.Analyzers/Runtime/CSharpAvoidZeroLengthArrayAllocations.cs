// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    /// <summary>
    /// Compatibility wrapper retained for callers that reference this type directly.
    /// The shared <see cref="AvoidZeroLengthArrayAllocationsAnalyzer"/> is registered for C# and Visual Basic.
    /// </summary>
    public sealed class CSharpAvoidZeroLengthArrayAllocationsAnalyzer : AvoidZeroLengthArrayAllocationsAnalyzer
    {
    }
}
