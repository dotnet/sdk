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
    internal static class FormatStyleCommand
    {
        private static readonly FormatStyleHandler s_styleHandler = new();

        internal static Symbol GetCommand()
        {
            var command = new Command("style", Resources.Run_code_style_analyzers_and_apply_fixes)
            {
                DiagnosticsOption,
                SeverityOption,
            };
            command.AddCommonOptions();
            command.Handler = s_styleHandler;
            return command;
        }

        private class FormatStyleHandler : ICommandHandler
        {
            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var parseResult = context.ParseResult;
                var formatOptions = parseResult.ParseVerbosityOption(FormatOptions.Instance);
                var logger = context.Console.SetupLogging(minimalLogLevel: formatOptions.LogLevel, minimalErrorLevel: LogLevel.Warning);
                formatOptions = parseResult.ParseCommonOptions(formatOptions, logger);
                formatOptions = parseResult.ParseWorkspaceOptions(formatOptions);

                if (parseResult.HasOption(SeverityOption) &&
                    parseResult.ValueForOption(SeverityOption) is string { Length: > 0 } styleSeverity)
                {
                    formatOptions = formatOptions with { CodeStyleSeverity = GetSeverity(styleSeverity) };
                }

                if (parseResult.HasOption(DiagnosticsOption) &&
                    parseResult.ValueForOption(DiagnosticsOption) is string[] { Length: > 0 } diagnostics)
                {
                    formatOptions = formatOptions with { Diagnostics = diagnostics.ToImmutableHashSet() };
                }

                formatOptions = formatOptions with { FixCategory = FixCategory.CodeStyle };

                return await FormatAsync(formatOptions, logger, context.GetCancellationToken()).ConfigureAwait(false);
            }
        }
    }
}
