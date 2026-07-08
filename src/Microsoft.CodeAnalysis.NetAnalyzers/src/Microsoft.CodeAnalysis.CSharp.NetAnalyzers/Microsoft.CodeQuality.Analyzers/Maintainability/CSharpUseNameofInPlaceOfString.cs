// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeQuality.Analyzers.Maintainability;

namespace Microsoft.CodeQuality.CSharp.Analyzers.Maintainability
{
    /// <summary>
    /// CA1507: Use nameof to express symbol names
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpUseNameofInPlaceOfStringAnalyzer : UseNameofInPlaceOfStringAnalyzer
    {
        protected override bool IsApplicableToLanguageVersion(ParseOptions options)
        {
            return ((CSharpParseOptions)options).LanguageVersion >= LanguageVersion.CSharp6;
        }
    }
}
