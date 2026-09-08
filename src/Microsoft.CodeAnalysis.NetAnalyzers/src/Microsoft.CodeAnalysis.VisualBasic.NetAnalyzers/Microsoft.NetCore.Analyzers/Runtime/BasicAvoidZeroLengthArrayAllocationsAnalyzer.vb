' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.NetCore.Analyzers.Runtime

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime
    ''' <summary>
    ''' Compatibility wrapper retained for callers that reference this type directly.
    ''' The shared <see cref="AvoidZeroLengthArrayAllocationsAnalyzer"/> is registered for C# and Visual Basic.
    ''' </summary>
    Public NotInheritable Class BasicAvoidZeroLengthArrayAllocationsAnalyzer
        Inherits AvoidZeroLengthArrayAllocationsAnalyzer
    End Class
End Namespace
