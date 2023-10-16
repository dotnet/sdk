' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports Analyzer.Utilities.Extensions
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeQuality.Analyzers.Maintainability

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.Maintainability
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicMakeTypesInternal
        Inherits MakeTypesInternal(Of SyntaxKind)

        Protected Overrides ReadOnly Property TypeKinds As ImmutableArray(Of SyntaxKind) = ImmutableArray.Create(SyntaxKind.ClassStatement, SyntaxKind.StructureStatement, SyntaxKind.InterfaceStatement)
        Protected Overrides ReadOnly Property EnumKind As SyntaxKind = SyntaxKind.EnumStatement
        Protected Overrides ReadOnly Property DelegateKinds As ImmutableArray(Of SyntaxKind) = ImmutableArray.Create(SyntaxKind.DelegateFunctionStatement, SyntaxKind.DelegateSubStatement)

        Protected Overrides Sub AnalyzeTypeDeclaration(context As SyntaxNodeAnalysisContext)
            Dim type = DirectCast(context.Node, TypeStatementSyntax)
            ReportIfPublic(context, type.Modifiers, type.Identifier)
        End Sub

        Protected Overrides Sub AnalyzeEnumDeclaration(context As SyntaxNodeAnalysisContext)
            Dim enumStatement = DirectCast(context.Node, EnumStatementSyntax)
            ReportIfPublic(context, enumStatement.Modifiers, enumStatement.Identifier)
        End Sub

        Protected Overrides Sub AnalyzeDelegateDeclaration(context As SyntaxNodeAnalysisContext)
            Dim delegateStatement = DirectCast(context.Node, DelegateStatementSyntax)
            ReportIfPublic(context, delegateStatement.Modifiers, delegateStatement.Identifier)
        End Sub

        Private Shared Sub ReportIfPublic(context As SyntaxNodeAnalysisContext, modifiers As SyntaxTokenList, identifier As SyntaxToken)
            If modifiers.Any(SyntaxKind.PublicKeyword) Then
                context.ReportDiagnostic(identifier.CreateDiagnostic(Rule))
            End If
        End Sub
    End Class
End Namespace