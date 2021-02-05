' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Operations
Imports Microsoft.NetCore.Analyzers.Tasks
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Tasks
    <Diagnostics.DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class VisualBasicDoNotUseWhenAllOrWaitAllWithSingleArgument
        Inherits DoNotUseWhenAllOrWaitAllWithSingleArgument

        Protected Overrides Function IsSingleTaskArgument(invocation As IInvocationOperation, taskType As INamedTypeSymbol, task1Type As INamedTypeSymbol) As Boolean
            Dim invocationExpressionSyntax = TryCast(invocation.Syntax, InvocationExpressionSyntax)

            If invocationExpressionSyntax Is Nothing Then
                Return False
            End If

            If invocationExpressionSyntax.ArgumentList.Arguments.Count <> 1 Then
                Return False
            End If

            Dim semanticModel = invocation.SemanticModel
            Dim typeInfo = semanticModel.GetTypeInfo(invocationExpressionSyntax.ArgumentList.Arguments(0).GetExpression())

            ' Check non generic task type
            If taskType.Equals(typeInfo.Type, SymbolEqualityComparer.Default) Then
                Return True
            End If

            ' Check generic task type
            Dim namedTypeSymbol = TryCast(typeInfo.Type, INamedTypeSymbol)

            If namedTypeSymbol Is Nothing Then
                Return False
            End If


            Return namedTypeSymbol.Arity = 1 And task1Type.Equals(namedTypeSymbol.ConstructedFrom, SymbolEqualityComparer.Default)
        End Function
    End Class
End Namespace
