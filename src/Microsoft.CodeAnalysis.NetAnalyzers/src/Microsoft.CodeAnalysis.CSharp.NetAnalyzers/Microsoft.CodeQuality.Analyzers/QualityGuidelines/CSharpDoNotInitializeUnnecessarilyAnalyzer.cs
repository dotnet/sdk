// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeQuality.Analyzers.QualityGuidelines;

namespace Microsoft.CodeQuality.CSharp.Analyzers.QualityGuidelines
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpDoNotInitializeUnnecessarilyAnalyzer : DoNotInitializeUnnecessarilyAnalyzer
    {
        protected override bool IsNullSuppressed(IOperation op)
            => op.Syntax?.Parent?.RawKind == (int)CodeAnalysis.CSharp.SyntaxKind.SuppressNullableWarningExpression;
    }
}
