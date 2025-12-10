' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports System.Threading
Imports Analyzer.Utilities.Extensions
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Editing
Imports Microsoft.CodeAnalysis.Operations
Imports Microsoft.CodeAnalysis.Simplification
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Usage
Imports Microsoft.NetCore.Analyzers.Usage.PreferGenericOverloadsAnalyzer

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Usage
    ''' <summary>
    ''' CA2263: <inheritdoc cref="NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources.PreferGenericOverloadsTitle"/>
    ''' </summary>
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicPreferGenericOverloadsFixer
        Inherits PreferGenericOverloadsFixer

        Protected Overrides Async Function ReplaceWithGenericCallAsync(document As Document,
                                                                       invocation As IInvocationOperation,
                                                                       cancellationToken As CancellationToken) As Task(Of Document)
            Dim invocationContext As RuntimeTypeInvocationContext = Nothing

            If Not RuntimeTypeInvocationContext.TryGetContext(invocation, invocationContext) Then
                Return document
            End If

            Dim modifiedInvocationSyntax = BasicPreferGenericOverloadsAnalyzer.GetModifiedInvocationSyntax(invocationContext)

            If TypeOf modifiedInvocationSyntax IsNot InvocationExpressionSyntax Then
                Return document
            End If

            ' Analyzers are not allowed to have a reference to Simplifier, so add the additional annotation here instead.
            Dim invocationExpressionSyntax = CType(modifiedInvocationSyntax, InvocationExpressionSyntax)
            invocationExpressionSyntax = invocationExpressionSyntax.WithExpression(invocationExpressionSyntax.Expression.WithAdditionalAnnotations(Simplifier.Annotation))

            Dim editor = Await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(False)

            If TypeOf invocationContext.Parent Is IConversionOperation And
               TypeOf invocationContext.Parent.Syntax Is CastExpressionSyntax And
               invocationContext.SemanticModel IsNot Nothing Then
                Dim conversionOperation = CType(invocationContext.Parent, IConversionOperation)
                Dim castExpressionSyntax = CType(invocationContext.Parent.Syntax, CastExpressionSyntax)
                Dim typeInfo = invocationContext.Invocation.SemanticModel.GetSpeculativeTypeInfo(invocationContext.Syntax.SpanStart,
                                                                                                 invocationExpressionSyntax,
                                                                                                 SpeculativeBindingOption.BindAsExpression)
                If typeInfo.ConvertedType.IsAssignableTo(conversionOperation.Type, invocationContext.SemanticModel.Compilation) Then
                    ' Add a simplifier annotation to the parent to remove no longer needed parentheses.
                    If TypeOf castExpressionSyntax.Parent Is ParenthesizedExpressionSyntax Then
                        Dim parenthesizedExpressionSyntax = CType(castExpressionSyntax.Parent, ParenthesizedExpressionSyntax)

                        editor.ReplaceNode(parenthesizedExpressionSyntax,
                                           parenthesizedExpressionSyntax _
                                               .ReplaceNode(castExpressionSyntax,
                                                            castExpressionSyntax.Expression _
                                                                .ReplaceNode(invocationContext.Syntax, invocationExpressionSyntax) _
                                                                .WithTriviaFrom(castExpressionSyntax)) _
                                               .WithAdditionalAnnotations(Simplifier.Annotation))
                    Else
                        editor.ReplaceNode(castExpressionSyntax,
                                           castExpressionSyntax.Expression _
                                               .ReplaceNode(invocationContext.Syntax, invocationExpressionSyntax) _
                                               .WithTriviaFrom(castExpressionSyntax))
                    End If
                Else
                    editor.ReplaceNode(invocationContext.Syntax, invocationExpressionSyntax)
                End If
            Else
                editor.ReplaceNode(invocationContext.Syntax, invocationExpressionSyntax)
            End If

            Return document.WithSyntaxRoot(editor.GetChangedRoot())
        End Function
    End Class
End Namespace
