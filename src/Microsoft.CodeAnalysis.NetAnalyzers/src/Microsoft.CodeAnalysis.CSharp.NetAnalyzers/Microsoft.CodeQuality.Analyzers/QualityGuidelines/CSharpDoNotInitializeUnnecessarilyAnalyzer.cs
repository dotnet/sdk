// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
