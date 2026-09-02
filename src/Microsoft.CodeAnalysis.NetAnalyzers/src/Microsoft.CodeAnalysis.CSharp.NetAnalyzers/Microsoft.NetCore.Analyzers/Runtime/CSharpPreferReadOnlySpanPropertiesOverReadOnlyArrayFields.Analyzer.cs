// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpPreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsAnalyzer
        : PreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsAnalyzer
    {
        protected override bool IsApplicableToLanguageVersion(ParseOptions options)
            => ((CSharpParseOptions)options).LanguageVersion >= LanguageVersion.CSharp7_2;
    }
}
