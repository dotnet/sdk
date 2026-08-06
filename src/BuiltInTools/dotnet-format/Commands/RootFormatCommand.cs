// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.Logging;
using static Microsoft.CodeAnalysis.Tools.FormatCommandCommon;

namespace Microsoft.CodeAnalysis.Tools.Commands
{
    internal static class RootFormatCommand
    {
        private static readonly FormatCommandDefaultHandler s_formatCommandHandler = new();

        public static RootCommand GetCommand()
        {
            var formatCommand = new RootCommand(Resources.Formats_code_to_match_editorconfig_settings)
            {
                FormatWhitespaceCommand.GetCommand(),
                FormatStyleCommand.GetCommand(),
                FormatAnalyzersCommand.GetCommand(),
                DiagnosticsOption,
                ExcludeDiagnosticsOption,
                SeverityOption,
            };
            formatCommand.AddCommonOptions();
            formatCommand.Action = s_formatCommandHandler;
            return formatCommand;
        }

        private class FormatCommandDefaultHandler : AsynchronousCommandLineAction
        {
            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken)
            {
                var formatOptions = parseResult.ParseVerbosityOption(FormatOptions.Instance);
                var logger = SetupLogging(minimalLogLevel: formatOptions.LogLevel, minimalErrorLevel: LogLevel.Warning);
                formatOptions = parseResult.ParseCommonOptions(formatOptions, logger);
                formatOptions = parseResult.ParseWorkspaceOptions(formatOptions);

                if (parseResult.GetResult(SeverityOption) is not null &&
                    parseResult.GetValue(SeverityOption) is string { Length: > 0 } defaultSeverity)
                {
                    formatOptions = formatOptions with { AnalyzerSeverity = GetSeverity(defaultSeverity) };
                    formatOptions = formatOptions with { CodeStyleSeverity = GetSeverity(defaultSeverity) };
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

                formatOptions = formatOptions with { FixCategory = FixCategory.Whitespace | FixCategory.CodeStyle | FixCategory.Analyzers };

                return await FormatAsync(formatOptions, logger, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
