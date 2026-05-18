// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Workspaces
{
    internal static class MSBuildWorkspaceLoader
    {
        // Used in tests for locking around MSBuild invocations
        internal static readonly SemaphoreSlim Guard = new SemaphoreSlim(1, 1);

        public static async Task<Workspace?> LoadAsync(
            string solutionOrProjectPath,
            WorkspaceType workspaceType,
            string? binaryLogPath,
            bool logWorkspaceWarnings,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var properties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // This property ensures that XAML files will be compiled in the current AppDomain
                // rather than a separate one. Any tasks isolated in AppDomains or tasks that create
                // AppDomains will likely not work due to https://github.com/Microsoft/MSBuildLocator/issues/16.
                { "AlwaysCompileMarkupFilesInSeparateDomain", bool.FalseString },
            };

            var workspace = MSBuildWorkspace.Create(properties);

            Build.Framework.ILogger? binlog = null;
            if (binaryLogPath is not null)
            {
                binlog = new Build.Logging.BinaryLogger()
                {
                    Parameters = binaryLogPath,
                    Verbosity = Build.Framework.LoggerVerbosity.Diagnostic,
                };
            }

            if (workspaceType == WorkspaceType.Solution)
            {
                await workspace.OpenSolutionAsync(solutionOrProjectPath, msbuildLogger: binlog, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else
            {
                try
                {
                    await workspace.OpenProjectAsync(solutionOrProjectPath, msbuildLogger: binlog, cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (InvalidOperationException)
                {
                    logger.LogError(Resources.Could_not_format_0_Format_currently_supports_only_CSharp_and_Visual_Basic_projects, solutionOrProjectPath);
                    workspace.Dispose();
                    return null;
                }
            }

            LogWorkspaceDiagnostics(logger, logWorkspaceWarnings, workspace.Diagnostics);

            return workspace;

            static void LogWorkspaceDiagnostics(ILogger logger, bool logWorkspaceWarnings, ImmutableList<WorkspaceDiagnostic> diagnostics)
            {
                if (!logWorkspaceWarnings)
                {
                    if (!diagnostics.IsEmpty)
                    {
                        logger.LogWarning(Resources.Warnings_were_encountered_while_loading_the_workspace_Set_the_verbosity_option_to_the_diagnostic_level_to_log_warnings);
                    }

                    return;
                }

                foreach (var diagnostic in diagnostics)
                {
                    if (diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
                    {
                        logger.LogError(diagnostic.Message);
                    }
                    else
                    {
                        logger.LogWarning(diagnostic.Message);
                    }
                }
            }
        }
    }
}
