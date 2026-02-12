// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeQuality.Analyzers.QualityGuidelines;

namespace Microsoft.CodeQuality.CSharp.Analyzers.QualityGuidelines
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpUseLiteralsWhereAppropriate : UseLiteralsWhereAppropriateAnalyzer
    {
        protected override bool IsConstantInterpolatedStringSupported(ParseOptions compilation)
            => ((CSharpParseOptions)compilation).LanguageVersion > (LanguageVersion)900; // Starting with C# 10 and above.
    }
}
