' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines

    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicDoNotDirectlyAwaitATask
        Inherits DoNotDirectlyAwaitATaskAnalyzer
    End Class
End Namespace