' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.NetCore.Analyzers.Runtime

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime

    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicDetectPreviewFeatureAnalyzer

        Inherits DetectPreviewFeatureAnalyzer

        Protected Overrides Function GetPreviewInterfaceNodeForTypeImplementingPreviewInterface(typeSymbol As ISymbol, previewInterfaceSymbol As ISymbol) As SyntaxNode
            Return Nothing
        End Function
    End Class

End Namespace