// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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