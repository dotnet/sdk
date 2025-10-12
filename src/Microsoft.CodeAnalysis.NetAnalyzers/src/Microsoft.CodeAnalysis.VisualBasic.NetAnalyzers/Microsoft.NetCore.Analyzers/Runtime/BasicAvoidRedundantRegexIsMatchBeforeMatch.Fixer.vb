' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports System.Composition
Imports System.Threading
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeActions
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Editing
Imports Microsoft.CodeAnalysis.Formatting
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Runtime

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime

    ''' <summary>
    ''' CA2027: <inheritdoc cref="NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources.AvoidRedundantRegexIsMatchBeforeMatchMessage"/>
    ''' </summary>
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicAvoidRedundantRegexIsMatchBeforeMatchFixer
        Inherits CodeFixProvider

        Public Overrides ReadOnly Property FixableDiagnosticIds As ImmutableArray(Of String) = ImmutableArray.Create(AvoidRedundantRegexIsMatchBeforeMatch.RuleId)

        Public Overrides Function GetFixAllProvider() As FixAllProvider
            Return WellKnownFixAllProviders.BatchFixer
        End Function

        Public Overrides Async Function RegisterCodeFixesAsync(context As CodeFixContext) As Task
            Dim root = Await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(False)
            If root Is Nothing Then
                Return
            End If

            Dim diagnostic = context.Diagnostics(0)
            Dim node = root.FindNode(context.Span, getInnermostNodeForTie:=True)

            If Not TypeOf node Is InvocationExpressionSyntax Then
                Return
            End If

            Dim isMatchInvocation = DirectCast(node, InvocationExpressionSyntax)

            ' Find the If statement that contains this IsMatch call
            Dim ifStatement = node.FirstAncestorOrSelf(Of MultiLineIfBlockSyntax)()
            If ifStatement Is Nothing Then
                ' Try single-line if
                Dim singleLineIf = node.FirstAncestorOrSelf(Of SingleLineIfStatementSyntax)()
                If singleLineIf Is Nothing Then
                    Return
                End If
            End If

            context.RegisterCodeFix(
                CodeAction.Create(
                    NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources.AvoidRedundantRegexIsMatchBeforeMatchFix,
                    Function(ct) RemoveRedundantIsMatchAsync(context.Document, root, ifStatement, isMatchInvocation, ct),
                    equivalenceKey:=NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources.AvoidRedundantRegexIsMatchBeforeMatchFix),
                diagnostic)
        End Function

        Private Shared Async Function RemoveRedundantIsMatchAsync(
            document As Document,
            root As SyntaxNode,
            ifStatement As MultiLineIfBlockSyntax,
            isMatchInvocation As InvocationExpressionSyntax,
            cancellationToken As CancellationToken) As Task(Of Document)

            Dim editor = Await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(False)

            ' For VB, simpler approach: just remove the if statement for early return pattern
            ' Or transform to check Match().Success in the condition
            Dim isNegated = TypeOf ifStatement.IfStatement.Condition Is UnaryExpressionSyntax AndAlso
                            DirectCast(ifStatement.IfStatement.Condition, UnaryExpressionSyntax).IsKind(SyntaxKind.NotExpression)

            If isNegated AndAlso IsEarlyReturnPattern(ifStatement) Then
                editor.RemoveNode(ifStatement)
            End If

            Return editor.GetChangedDocument()
        End Function

        Private Shared Function IsEarlyReturnPattern(ifStatement As MultiLineIfBlockSyntax) As Boolean
            For Each statement In ifStatement.Statements
                If TypeOf statement Is ReturnStatementSyntax OrElse
                   TypeOf statement Is ThrowStatementSyntax OrElse
                   TypeOf statement Is ExitStatementSyntax Then
                    Return True
                End If
            Next
            Return False
        End Function

    End Class

End Namespace
