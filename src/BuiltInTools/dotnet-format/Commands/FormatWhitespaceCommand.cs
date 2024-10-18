// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using Microsoft.Extensions.Logging;
using static Microsoft.CodeAnalysis.Tools.FormatCommandCommon;

namespace Microsoft.CodeAnalysis.Tools.Commands
{
    internal static class FormatWhitespaceCommand
    {
        // This delegate should be kept in Sync with the FormatCommand options and argument names
        // so that values bind correctly.
        internal delegate Task<int> Handler(
            bool folder,
            string? workspace,
            bool noRestore,
            bool check,
            string[] include,
            string[] exclude,
            bool includeGenerated,
            string? verbosity,
            string? binarylog,
            string? report,
            IConsole console);

        private static readonly FormatWhitespaceHandler s_formattingHandler = new();

        internal static CliCommand GetCommand()
        {
            var command = new CliCommand("whitespace", Resources.Run_whitespace_formatting)
            {
                FolderOption
            };
            command.AddCommonOptions();
            command.Validators.Add(EnsureFolderNotSpecifiedWithNoRestore);
            command.Validators.Add(EnsureFolderNotSpecifiedWhenLoggingBinlog);
            command.Action = s_formattingHandler;
            return command;
        }

        internal static void EnsureFolderNotSpecifiedWithNoRestore(CommandResult symbolResult)
        {
            var folder = symbolResult.GetValue(FolderOption);
            var noRestore = symbolResult.GetResult(NoRestoreOption);
            if (folder && noRestore != null)
            {
                symbolResult.AddError(Resources.Cannot_specify_the_folder_option_with_no_restore);
            }
        }

        internal static void EnsureFolderNotSpecifiedWhenLoggingBinlog(CommandResult symbolResult)
        {
            var folder = symbolResult.GetValue(FolderOption);
            var binarylog = symbolResult.GetResult(BinarylogOption);
            if (folder && binarylog is not null && !binarylog.Implicit)
            {
                symbolResult.AddError(Resources.Cannot_specify_the_folder_option_when_writing_a_binary_log);
            }
        }

        private class FormatWhitespaceHandler : AsynchronousCliAction
        {
            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken)
            {
                var formatOptions = parseResult.ParseVerbosityOption(FormatOptions.Instance);
                var logger = new SystemConsole().SetupLogging(minimalLogLevel: formatOptions.LogLevel, minimalErrorLevel: LogLevel.Warning);
                formatOptions = parseResult.ParseCommonOptions(formatOptions, logger);
                formatOptions = parseResult.ParseWorkspaceOptions(formatOptions);

                formatOptions = formatOptions with { FixCategory = FixCategory.Whitespace };

                return await FormatAsync(formatOptions, logger, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
