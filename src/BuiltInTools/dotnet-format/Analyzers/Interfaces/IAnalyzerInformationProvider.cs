// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
