// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.NetCore.Analyzers.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.CSharp.Analyzers.InteropServices
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CSharpAlwaysConsumeTheValueReturnedByMethodsMarkedWithPreserveSigAttributeAnalyzer
        : AlwaysConsumeTheValueReturnedByMethodsMarkedWithPreserveSigAttributeAnalyzer<SyntaxKind>
    {
        protected override SyntaxKind InvocationExpressionSyntaxKind => SyntaxKind.InvocationExpression;

        protected override bool IsExpressionStatementSyntaxKind(int rawKind)
        {
            return (SyntaxKind)rawKind == SyntaxKind.ExpressionStatement;
        }
    }
}
