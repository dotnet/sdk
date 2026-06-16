' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.NetCore.Analyzers.Tasks
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Tasks
    ''' <summary>
    ''' RS0018: Do not create tasks without passing a TaskScheduler
    ''' </summary>
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicDoNotCreateTasksWithoutPassingATaskSchedulerFixer
        Inherits DoNotCreateTasksWithoutPassingATaskSchedulerFixer

    End Class
End Namespace
