// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.Performance
{
    /// <summary>
    /// CA1855: C# implementation of use Span.Clear instead of Span.Fill(default)
    /// Implements the <see cref="UseSpanClearInsteadOfFillAnalyzer" />
    /// </summary>
    /// <seealso cref="UseSpanClearInsteadOfFillFixer"/>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpUseSpanClearInsteadOfFillAnalyzer : UseSpanClearInsteadOfFillAnalyzer
    {
    }
}