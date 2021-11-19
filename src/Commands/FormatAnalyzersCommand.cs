// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using static Microsoft.CodeAnalysis.Tools.FormatCommandCommon;

namespace Microsoft.CodeAnalysis.Tools.Commands
{
    internal static class FormatAnalyzersCommand
    {
        private static readonly FormatAnalyzersHandler s_analyzerHandler = new();

        internal static Symbol GetCommand()
        {
            var command = new Command("analyzers", Resources.Run_3rd_party_analyzers__and_apply_fixes)
            {
                DiagnosticsOption,
                SeverityOption,
            };
            command.AddCommonOptions();
            command.Handler = s_analyzerHandler;
            return command;
        }

        private class FormatAnalyzersHandler : ICommandHandler
        {
            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var parseResult = context.ParseResult;
                var formatOptions = parseResult.ParseVerbosityOption(FormatOptions.Instance);
                var logger = context.Console.SetupLogging(minimalLogLevel: formatOptions.LogLevel, minimalErrorLevel: LogLevel.Warning);
                formatOptions = parseResult.ParseCommonOptions(formatOptions, logger);
                formatOptions = parseResult.ParseWorkspaceOptions(formatOptions);

                if (parseResult.HasOption(SeverityOption) &&
                    parseResult.GetValueForOption(SeverityOption) is string { Length: > 0 } analyzerSeverity)
                {
                    formatOptions = formatOptions with { AnalyzerSeverity = GetSeverity(analyzerSeverity) };
                }

                if (parseResult.HasOption(DiagnosticsOption) &&
                    parseResult.GetValueForOption(DiagnosticsOption) is string[] { Length: > 0 } diagnostics)
                {
                    formatOptions = formatOptions with { Diagnostics = diagnostics.ToImmutableHashSet() };
                }

                formatOptions = formatOptions with { FixCategory = FixCategory.Analyzers };

                return await FormatAsync(formatOptions, logger, context.GetCancellationToken()).ConfigureAwait(false);
            }
        }
    }
}
