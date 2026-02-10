' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.QualityGuidelines

    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Partial Friend NotInheritable Class BasicAvoidMultipleEnumerationsAnalyzer
        Inherits AvoidMultipleEnumerations

        Protected Overrides Function IsExpressionOfForEachStatement(syntax As SyntaxNode) As Boolean
            Dim parent = TryCast(syntax.Parent, ForEachStatementSyntax)
            Return parent IsNot Nothing AndAlso parent.Expression.Equals(syntax)
        End Function
    End Class
End Namespace