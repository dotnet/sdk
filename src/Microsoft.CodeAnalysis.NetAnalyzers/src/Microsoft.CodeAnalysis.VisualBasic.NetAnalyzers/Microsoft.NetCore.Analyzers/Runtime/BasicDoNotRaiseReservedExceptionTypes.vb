' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.NetCore.Analyzers.Runtime
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime
    ''' <summary>
    ''' CA2201: Do not raise reserved exception types
    ''' </summary>
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicDoNotRaiseReservedExceptionTypesAnalyzer
        Inherits DoNotRaiseReservedExceptionTypesAnalyzer(Of SyntaxKind, ObjectCreationExpressionSyntax)

        Public Overrides ReadOnly Property ObjectCreationExpressionKind As SyntaxKind
            Get
                Return SyntaxKind.ObjectCreationExpression
            End Get
        End Property

        Public Overrides Function GetTypeSyntaxNode(node As ObjectCreationExpressionSyntax) As SyntaxNode
            Return node.Type
        End Function
    End Class
End Namespace