' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeQuality.Analyzers.Documentation

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.Documentation
    ''' <summary>
    ''' CA1200: Avoid using cref tags with a prefix
    ''' </summary>
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicAvoidUsingCrefTagsWithAPrefixAnalyzer
        Inherits AvoidUsingCrefTagsWithAPrefixAnalyzer

        Public Overrides Sub Initialize(context As AnalysisContext)
            context.EnableConcurrentExecution()
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)

            context.RegisterSyntaxNodeAction(AddressOf AnalyzeXmlAttribute, SyntaxKind.XmlAttribute)
        End Sub

        Private Shared Sub AnalyzeXmlAttribute(context As SyntaxNodeAnalysisContext)
            Dim node = DirectCast(context.Node, XmlAttributeSyntax)

            If DirectCast(node.Name, XmlNameSyntax).LocalName.Text = "cref" Then
                Dim value = TryCast(node.Value, XmlStringSyntax)

                If value IsNot Nothing Then
                    ProcessAttribute(context, value.TextTokens)
                End If
            End If
        End Sub
    End Class
End Namespace