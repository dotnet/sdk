' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Threading
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Runtime

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicInitializeStaticFieldsInlineAnalyzer
        Inherits InitializeStaticFieldsInlineAnalyzer(Of SyntaxKind)

        Protected Overrides ReadOnly Property AssignmentNodeKind As SyntaxKind
            Get
                Return SyntaxKind.SimpleAssignmentStatement
            End Get
        End Property

        Protected Overrides Function InitialiesStaticField(node As SyntaxNode, semanticModel As SemanticModel, cancellationToken As CancellationToken) As Boolean
            Dim assignmentStatement = DirectCast(node, AssignmentStatementSyntax)

            If assignmentStatement.FirstAncestorOrSelf(Of AddRemoveHandlerStatementSyntax)(AddressOf IsAddHandler) IsNot Nothing Then
                Return False
            End If

            Dim leftSymbol = TryCast(semanticModel.GetSymbolInfo(assignmentStatement.Left, cancellationToken).Symbol, IFieldSymbol)
            Return leftSymbol IsNot Nothing AndAlso leftSymbol.IsStatic
        End Function

        Private Function IsAddHandler(ByVal assignment As AddRemoveHandlerStatementSyntax) As Boolean
            Return assignment.IsKind(SyntaxKind.AddHandlerStatement)
        End Function
    End Class
End Namespace