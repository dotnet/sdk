' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeQuality.Analyzers.Maintainability

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.Maintainability
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public Class BasicAvoidDeadConditionalCode
        Inherits AvoidDeadConditionalCode

        Protected Overrides Function IsSwitchArmExpressionWithWhenClause(node As SyntaxNode) As Boolean
            Return False
        End Function
    End Class
End Namespace

