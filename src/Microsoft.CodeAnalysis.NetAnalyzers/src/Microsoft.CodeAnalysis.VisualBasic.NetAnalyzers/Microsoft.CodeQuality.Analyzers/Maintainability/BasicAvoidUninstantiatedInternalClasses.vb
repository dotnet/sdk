' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Collections.Concurrent
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeQuality.Analyzers.Maintainability

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.Maintainability
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public Class BasicAvoidUninstantiatedInternalClasses
        Inherits AvoidUninstantiatedInternalClassesAnalyzer

        <System.Diagnostics.CodeAnalysis.SuppressMessage("MicrosoftCodeAnalysisPerformance", "RS1012:Start action has no registered actions.", Justification:="End action is registered in parent class.")>
        Public Overrides Sub RegisterLanguageSpecificChecks(context As CompilationStartAnalysisContext, instantiatedTypes As ConcurrentDictionary(Of INamedTypeSymbol, Object))
            ' No Visual Basic specific check
        End Sub
    End Class
End Namespace

