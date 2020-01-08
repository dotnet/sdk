' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines
    ''' <summary>
    ''' CA2234: Pass system uri objects instead of strings
    ''' </summary>
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicPassSystemUriObjectsInsteadOfStringsAnalyzer
        Inherits PassSystemUriObjectsInsteadOfStringsAnalyzer

        Protected Overrides Function GetInvocationExpression(node As SyntaxNode) As SyntaxNode
            Dim invocationNode = TryCast(node, InvocationExpressionSyntax)
            Return invocationNode?.Expression
        End Function
    End Class
End Namespace