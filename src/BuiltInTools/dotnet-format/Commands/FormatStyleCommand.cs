// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.Logging;
using static Microsoft.CodeAnalysis.Tools.FormatCommandCommon;

namespace Microsoft.CodeAnalysis.Tools.Commands
{
    internal static class FormatStyleCommand
    {
        private static readonly FormatStyleHandler s_styleHandler = new();

        internal static CliCommand GetCommand()
        {
            var command = new CliCommand("style", Resources.Run_code_style_analyzers_and_apply_fixes)
            {
                DiagnosticsOption,
                ExcludeDiagnosticsOption,
                SeverityOption,
            };
            command.AddCommonOptions();
            command.Action = s_styleHandler;
            return command;
        }

        private class FormatStyleHandler : AsynchronousCliAction
        {
            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken)
            {
                var formatOptions = parseResult.ParseVerbosityOption(FormatOptions.Instance);
                var logger = SetupLogging(minimalLogLevel: formatOptions.LogLevel, minimalErrorLevel: LogLevel.Warning);
                formatOptions = parseResult.ParseCommonOptions(formatOptions, logger);
                formatOptions = parseResult.ParseWorkspaceOptions(formatOptions);

                if (parseResult.GetResult(SeverityOption) is not null &&
                    parseResult.GetValue(SeverityOption) is string { Length: > 0 } styleSeverity)
                {
                    formatOptions = formatOptions with { CodeStyleSeverity = GetSeverity(styleSeverity) };
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

                formatOptions = formatOptions with { FixCategory = FixCategory.CodeStyle };

                return await FormatAsync(formatOptions, logger, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
