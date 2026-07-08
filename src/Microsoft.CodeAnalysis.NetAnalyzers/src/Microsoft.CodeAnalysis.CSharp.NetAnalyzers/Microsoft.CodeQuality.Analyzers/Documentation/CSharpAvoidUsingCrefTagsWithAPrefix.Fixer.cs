// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeQuality.Analyzers.Documentation;

namespace Microsoft.CodeQuality.CSharp.Analyzers.Documentation
{
    /// <summary>
    /// CA1200: Avoid using cref tags with a prefix
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public class CSharpAvoidUsingCrefTagsWithAPrefixFixer : AvoidUsingCrefTagsWithAPrefixFixer
    {
    }
}