// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
