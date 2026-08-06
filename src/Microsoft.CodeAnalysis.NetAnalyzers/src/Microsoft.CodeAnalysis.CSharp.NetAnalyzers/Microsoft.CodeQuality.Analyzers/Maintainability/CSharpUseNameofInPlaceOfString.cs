// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
