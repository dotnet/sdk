' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.NetCore.Analyzers.Security
Imports Microsoft.CodeAnalysis.VisualBasic.TypedConstantExtensions

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public Class BasicDataSetDataTableInSerializableObjectGraphAnalyzer
        Inherits DataSetDataTableInSerializableObjectGraphAnalyzer

        Protected Overrides Function ToString(typedConstant As TypedConstant) As String
            Return typedConstant.ToVisualBasicString()
        End Function
    End Class
End Namespace
