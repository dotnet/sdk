// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeQuality.Analyzers.Maintainability;

namespace Microsoft.CodeQuality.CSharp.Analyzers.Maintainability
{
    /// <summary>
    /// CA1516: Use cross-platform intrinsics
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpUseCrossPlatformIntrinsicsAnalyzer : UseCrossPlatformIntrinsicsAnalyzer
    {
        protected override bool IsSupported(IInvocationOperation invocation, RuleKind ruleKind)
        {
            if (invocation.Syntax is not InvocationExpressionSyntax)
            {
                return false;
            }

            return base.IsSupported(invocation, ruleKind);
        }
    }
}
