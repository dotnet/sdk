// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.Logging;
using static Microsoft.CodeAnalysis.Tools.FormatCommandCommon;

namespace Microsoft.CodeAnalysis.Tools.Commands
{
    internal static class FormatAnalyzersCommand
    {
        private static readonly FormatAnalyzersHandler s_analyzerHandler = new();

        internal static CliCommand GetCommand()
        {
            var command = new CliCommand("analyzers", Resources.Run_3rd_party_analyzers__and_apply_fixes)
            {
                DiagnosticsOption,
                ExcludeDiagnosticsOption,
                SeverityOption,
            };
            command.AddCommonOptions();
            command.Action = s_analyzerHandler;
            return command;
        }

        private class FormatAnalyzersHandler : AsynchronousCliAction
        {
            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken)
            {
                var formatOptions = parseResult.ParseVerbosityOption(FormatOptions.Instance);
                var logger = SetupLogging(minimalLogLevel: formatOptions.LogLevel);
                formatOptions = parseResult.ParseCommonOptions(formatOptions, logger);
                formatOptions = parseResult.ParseWorkspaceOptions(formatOptions);

                if (parseResult.GetResult(SeverityOption) is not null &&
                    parseResult.GetValue(SeverityOption) is string { Length: > 0 } analyzerSeverity)
                {
                    formatOptions = formatOptions with { AnalyzerSeverity = GetSeverity(analyzerSeverity) };
                }

                if (parseResult.GetResult(DiagnosticsOption) is not null &&
                    parseResult.GetValue(DiagnosticsOption) is string[] { Length: > 0 } diagnostics)
                {
                    formatOptions = formatOptions with { Diagnostics = diagnostics.ToImmutableHashSet() };
                }

                if (parseResult.GetResult(ExcludeDiagnosticsOption) is not null &&
                    parseResult.GetValue(ExcludeDiagnosticsOption) is string[] { Length: > 0 } excludeDiagnostics)
                {
                    formatOptions = formatOptions with { ExcludeDiagnostics = excludeDiagnostics.ToImmutableHashSet() };
                }

                formatOptions = formatOptions with { FixCategory = FixCategory.Analyzers };

                return await FormatAsync(formatOptions, logger, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
