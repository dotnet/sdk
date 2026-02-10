// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Composition;
using Microsoft.CodeQuality.Analyzers.ApiReview;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Microsoft.CodeQuality.CSharp.Analyzers.ApiReview
{
    /// <summary>
    /// CA2001: Avoid calling problematic methods
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public class CSharpAvoidCallingProblematicMethodsFixer : AvoidCallingProblematicMethodsFixer
    {
    }
}