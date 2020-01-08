' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.NetCore.Analyzers.InteropServices
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.VisualBasic

Namespace Microsoft.NetCore.VisualBasic.Analyzers.InteropServices
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public Class BasicAlwaysConsumeTheValueReturnedByMethodsMarkedWithPreserveSigAttributeAnalyzer
        Inherits AlwaysConsumeTheValueReturnedByMethodsMarkedWithPreserveSigAttributeAnalyzer(Of SyntaxKind)

        Protected Overrides ReadOnly Property InvocationExpressionSyntaxKind As SyntaxKind
            Get
                Return SyntaxKind.InvocationExpression
            End Get
        End Property

        Protected Overrides Function IsExpressionStatementSyntaxKind(rawKind As Integer) As Boolean
            Return CType(rawKind, SyntaxKind) = SyntaxKind.ExpressionStatement
        End Function
    End Class
End Namespace
