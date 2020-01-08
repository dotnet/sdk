' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines
    ''' <summary>
    ''' CA1726: Use preferred terms
    ''' </summary>
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicUsePreferredTermsAnalyzer
        Inherits UsePreferredTermsAnalyzer

    End Class
End Namespace