// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeQuality.Analyzers.ApiReview;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.CSharp.Analyzers.ApiReview
{
    /// <summary>
    /// CA2001: Avoid calling problematic methods
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpAvoidCallingProblematicMethodsAnalyzer : AvoidCallingProblematicMethodsAnalyzer
    {
    }
}