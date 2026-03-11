' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.NetCore.Analyzers.Runtime
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime
    ''' <summary>
    ''' RS0007: Avoid zero-length array allocations.
    ''' </summary>
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicAvoidZeroLengthArrayAllocationsAnalyzer
        Inherits AvoidZeroLengthArrayAllocationsAnalyzer

        Protected Overrides Function IsAttributeSyntax(node As SyntaxNode) As Boolean
            Return TypeOf node Is AttributeSyntax
        End Function
    End Class
End Namespace