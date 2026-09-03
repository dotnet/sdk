// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1014: Mark assemblies with CLSCompliant
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public class CSharpMarkAssembliesWithClsCompliantFixer : MarkAssembliesWithClsCompliantFixer
    {
    }
}