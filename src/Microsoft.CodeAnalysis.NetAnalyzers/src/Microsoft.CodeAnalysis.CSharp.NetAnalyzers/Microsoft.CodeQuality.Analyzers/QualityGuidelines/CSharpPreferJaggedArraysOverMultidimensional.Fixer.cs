// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeQuality.Analyzers.QualityGuidelines;

namespace Microsoft.CodeQuality.CSharp.Analyzers.QualityGuidelines
{
    /// <summary>
    /// CA1814: Prefer jagged arrays over multidimensional
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpPreferJaggedArraysOverMultidimensionalFixer : PreferJaggedArraysOverMultidimensionalFixer
    {
    }
}