' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
        Protected Overrides Sub RegisterAttributeAnalyzer(context As CompilationStartAnalysisContext, onResourceFound As Action)
            context.RegisterSyntaxNodeAction(
                Sub(nc)
                    If Not CheckBasicAttribute(nc.Node) Then
                        Return
                    End If

                    If Not CheckResxGeneratedFile(nc.SemanticModel, nc.Node, DirectCast(nc.Node, AttributeSyntax).ArgumentList.Arguments(0).GetExpression(), nc.CancellationToken) Then
                        Return
                    End If

                    onResourceFound()
                End Sub, SyntaxKind.Attribute)
        End Sub

        Private Shared Function CheckBasicAttribute(node As SyntaxNode) As Boolean
            Dim attribute = TryCast(node, AttributeSyntax)
            Return (attribute?.Name?.GetLastToken().Text.Equals(GeneratedCodeAttribute, StringComparison.Ordinal) = True AndAlso
                attribute.ArgumentList.Arguments.Count > 0).GetValueOrDefault()
        End Function
    End Class
End Namespace