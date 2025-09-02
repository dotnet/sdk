' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Diagnostics.CodeAnalysis
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Usage

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Usage
    ''' <summary>
    ''' CA2263: <inheritdoc cref="NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources.PreferGenericOverloadsTitle"/>
    ''' </summary>
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicPreferGenericOverloadsAnalyzer
        Inherits PreferGenericOverloadsAnalyzer

        Protected Overrides Function TryGetModifiedInvocationSyntax(invocationContext As RuntimeTypeInvocationContext,
                                                                    <NotNullWhen(True)> ByRef modifiedInvocationSyntax As SyntaxNode) As Boolean
            modifiedInvocationSyntax = GetModifiedInvocationSyntax(invocationContext)

            Return modifiedInvocationSyntax IsNot Nothing
        End Function

        ' Expose as friend shared to allow the fixer to also call this method.
        Friend Shared Function GetModifiedInvocationSyntax(invocationContext As RuntimeTypeInvocationContext) As SyntaxNode
            If TypeOf invocationContext.Syntax IsNot InvocationExpressionSyntax Then
                Return Nothing
            End If

            Dim invocationSyntax = CType(invocationContext.Syntax, InvocationExpressionSyntax)
            Dim typeArgumentSyntax = invocationContext.TypeArguments.Select(Function(t) SyntaxFactory.ParseTypeName(t.ToDisplayString()))
            Dim otherArgumentsSyntax = invocationContext.OtherArguments _
                .Where(Function(a) a.ArgumentKind <> Operations.ArgumentKind.DefaultValue) _
                .Select(Function(a) a.Syntax) _
                .OfType(Of ArgumentSyntax)
            Dim methodNameSyntax =
                SyntaxFactory.GenericName(SyntaxFactory.Identifier(invocationContext.Method.Name),
                                          SyntaxFactory.TypeArgumentList(typeArgumentSyntax.ToArray()))
            Dim modifiedInvocationExpression = invocationSyntax.Expression

            If TypeOf modifiedInvocationExpression Is MemberAccessExpressionSyntax Then
                Dim memberAccessExpressionSyntax = CType(modifiedInvocationExpression, MemberAccessExpressionSyntax)
                modifiedInvocationExpression = memberAccessExpressionSyntax.WithName(methodNameSyntax)
            ElseIf TypeOf modifiedInvocationExpression Is IdentifierNameSyntax Then
                Dim identifierNameSyntax = CType(modifiedInvocationExpression, IdentifierNameSyntax)
                modifiedInvocationExpression = methodNameSyntax
            Else
                Return Nothing
            End If

            Return invocationSyntax _
                .WithExpression(modifiedInvocationExpression) _
                .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(otherArgumentsSyntax))) _
                .WithTriviaFrom(invocationSyntax)
        End Function
    End Class
End Namespace
