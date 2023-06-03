// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;

namespace Microsoft.DotNet.ApiCompat
{
    internal static class SuppressionFileHelper
    {
        /// <summary>
        /// Write the suppression file to disk and throw if a path isn't provided.
        /// </summary>
        public static void GenerateSuppressionFile(ISuppressionEngine suppressionEngine,
            ISuppressableLog log,
            bool preserveUnnecessarySuppressions,
            string[]? suppressionFiles,
            string? suppressionOutputFile)
        {
            // When a single suppression (input) file is passed in but no suppression output file, use the single input file.
            if (suppressionOutputFile == null && suppressionFiles?.Length == 1)
            {
                suppressionOutputFile = suppressionFiles[0];
            }

            if (suppressionOutputFile == null)
            {
                log.LogError(CommonResources.SuppressionsFileNotSpecified);
                return;
            }

            if (suppressionEngine.WriteSuppressionsToFile(suppressionOutputFile, preserveUnnecessarySuppressions))
            {
                log.LogMessage(MessageImportance.High,
                    string.Format(CommonResources.WroteSuppressions, suppressionOutputFile));
            }
        }

        /// <summary>
        /// Log whether or not we found breaking changes. If we are writing to a suppression file, no need to log anything.
        /// </summary>
        public static void LogApiCompatSuccessOrFailure(bool generateSuppressionFile, ISuppressableLog log)
        {
            if (log.HasLoggedSuppressions)
            {
                if (!generateSuppressionFile)
                {
                    log.LogError(Resources.BreakingChangesFoundRegenerateSuppressionFileCommandHelp);
                }
            }
            else
            {
                log.LogMessage(MessageImportance.Normal, CommonResources.NoBreakingChangesFound);
            }
        }

        /// <summary>
        /// Validate whether obsolete baseline suppressions exist and log those.
        /// </summary>
        public static void ValidateUnnecessarySuppressions(ISuppressionEngine suppressionEngine, ISuppressableLog log)
        {
            IReadOnlyCollection<Suppression> unnecessarySuppressions = suppressionEngine.GetUnnecessarySuppressions();
            if (unnecessarySuppressions.Count == 0)
            {
                return;
            }

            log.LogError(Resources.UnnecessarySuppressionsFoundRegenerateSuppressionFileCommandHelp);
            foreach (Suppression unnecessarySuppression in unnecessarySuppressions)
            {
                log.LogMessage(MessageImportance.Normal, "- " + unnecessarySuppression.ToString());
            }
        }
    }
}
