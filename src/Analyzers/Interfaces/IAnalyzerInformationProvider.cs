// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    interface IAnalyzerInformationProvider
    {
        DiagnosticSeverity GetSeverity(FormatOptions formatOptions);

        ImmutableArray<(DiagnosticAnalyzer Analyzer, CodeFixProvider? Fixer)> GetAnalyzersAndFixers(
            Solution solution,
            FormatOptions formatOptions,
            ILogger logger);
    }
}
