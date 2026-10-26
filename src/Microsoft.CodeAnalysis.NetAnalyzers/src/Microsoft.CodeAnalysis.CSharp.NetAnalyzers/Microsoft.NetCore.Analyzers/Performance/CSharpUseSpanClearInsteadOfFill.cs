// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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