' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.NetCore.Analyzers.Runtime
Imports Microsoft.CodeAnalysis.Operations
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime

    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicUseSpanBasedStringConcat : Inherits UseSpanBasedStringConcat

        Private Protected Overrides Function IsStringConcatOperation(binaryOperation As IBinaryOperation) As Boolean
            Return binaryOperation.OperatorKind = BinaryOperatorKind.Concatenate
        End Function
    End Class
End Namespace

