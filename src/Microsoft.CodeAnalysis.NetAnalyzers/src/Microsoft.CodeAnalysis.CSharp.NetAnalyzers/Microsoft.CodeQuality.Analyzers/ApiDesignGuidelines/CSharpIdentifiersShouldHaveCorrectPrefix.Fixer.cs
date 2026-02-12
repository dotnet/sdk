// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Composition;
using Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1715: Identifiers should have correct prefix
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public class CSharpIdentifiersShouldHaveCorrectPrefixFixer : IdentifiersShouldHaveCorrectPrefixFixer
    {
    }
}