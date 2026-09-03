' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.NetCore.Analyzers.Resources
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Resources
    ''' <summary>
    ''' CA1824: Mark assemblies with NeutralResourcesLanguageAttribute
    ''' </summary>
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicMarkAssembliesWithNeutralResourcesLanguageAnalyzer
        Inherits MarkAssembliesWithNeutralResourcesLanguageAnalyzer
        Protected Overrides Sub RegisterAttributeAnalyzer(context As CompilationStartAnalysisContext, shouldAnalyze As Func(Of Boolean), onResourceFound As Action(Of SyntaxNodeAnalysisContext), generatedCode As INamedTypeSymbol)
            context.RegisterSyntaxNodeAction(
                Sub(nc)
                    If Not shouldAnalyze() Then
                        Return
                    End If

                    Dim attributeSyntax = DirectCast(nc.Node, AttributeSyntax)
                    If Not CheckBasicAttribute(attributeSyntax) Then
                        Return
                    End If

                    If Not CheckResxGeneratedFile(nc.SemanticModel, attributeSyntax, attributeSyntax.ArgumentList.Arguments(0).GetExpression(), generatedCode, nc.CancellationToken) Then
                        Return
                    End If

                    onResourceFound(nc)
                End Sub, SyntaxKind.Attribute)
        End Sub

        Private Shared Function CheckBasicAttribute(attribute As AttributeSyntax) As Boolean
            Return (attribute?.Name?.GetLastToken().Text.Equals(GeneratedCodeAttribute, StringComparison.Ordinal) = True AndAlso
                attribute.ArgumentList.Arguments.Count > 0).GetValueOrDefault()
        End Function
    End Class
End Namespace