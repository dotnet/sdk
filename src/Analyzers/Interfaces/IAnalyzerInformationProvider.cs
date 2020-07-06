// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal interface IAnalyzerInformationProvider
    {
        DiagnosticSeverity GetSeverity(FormatOptions formatOptions);

        (ImmutableArray<DiagnosticAnalyzer> Analyzers, ImmutableArray<CodeFixProvider> Fixers) GetAnalyzersAndFixers(
            Solution solution,
            FormatOptions formatOptions,
            ILogger logger);
    }
}
