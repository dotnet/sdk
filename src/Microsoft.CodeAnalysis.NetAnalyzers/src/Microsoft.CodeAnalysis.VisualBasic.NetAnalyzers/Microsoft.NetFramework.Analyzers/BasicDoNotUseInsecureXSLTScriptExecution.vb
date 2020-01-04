' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.


Imports Microsoft.NetFramework.Analyzers
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.NetFramework.Analyzers.Helpers
Imports Microsoft.NetFramework.VisualBasic.Analyzers.Helpers

Namespace Microsoft.NetFramework.VisualBasic.Analyzers
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicDoNotUseInsecureXSLTScriptExecutionAnalyzer
        Inherits DoNotUseInsecureXSLTScriptExecutionAnalyzer(Of SyntaxKind)
        Protected Overrides Function GetAnalyzer(context As CodeBlockStartAnalysisContext(Of SyntaxKind), types As CompilationSecurityTypes) As SyntaxNodeAnalyzer
            Dim analyzer As New SyntaxNodeAnalyzer(types, BasicSyntaxNodeHelper.DefaultInstance)
            context.RegisterSyntaxNodeAction(AddressOf analyzer.AnalyzeNode, SyntaxKind.InvocationExpression,
                                                                             SyntaxKind.ObjectCreationExpression,
                                                                             SyntaxKind.SimpleAssignmentStatement,
                                                                             SyntaxKind.VariableDeclarator,
                                                                             SyntaxKind.NamedFieldInitializer)
            Return analyzer
        End Function
    End Class
End Namespace
