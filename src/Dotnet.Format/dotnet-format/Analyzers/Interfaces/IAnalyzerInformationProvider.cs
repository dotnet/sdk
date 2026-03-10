// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal interface IAnalyzerInformationProvider
    {
        DiagnosticSeverity GetSeverity(FormatOptions formatOptions);

        ImmutableDictionary<ProjectId, AnalyzersAndFixers> GetAnalyzersAndFixers(
            Workspace workspace,
            Solution solution,
            FormatOptions formatOptions,
            ILogger logger);
    }
}
