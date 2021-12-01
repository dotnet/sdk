' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.QualityGuidelines
    Friend Class BasicAvoidMultipleEnumerationsAnalyzer
        Private Class BasicAvoidMultipleEnumerationsHelpers
            Inherits AvoidMultipleEnumerationsHelpers

            Public Shared ReadOnly Instance As New BasicAvoidMultipleEnumerationsHelpers()

        End Class
    End Class
End Namespace