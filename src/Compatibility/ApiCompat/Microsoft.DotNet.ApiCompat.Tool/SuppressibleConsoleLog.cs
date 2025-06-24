// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;

namespace Microsoft.DotNet.ApiCompat.Tool
{
    /// <summary>
    /// Class that can log Suppressions to the Console, by implementing ConsoleLog and ISuppressibleLog.
    /// </summary>
    internal sealed class SuppressibleConsoleLog(ISuppressionEngine suppressionEngine,
        MessageImportance messageImportance,
        string? noWarn = null) : ConsoleLog(messageImportance, noWarn), ISuppressibleLog
    {
        /// <inheritdoc />
        public bool HasLoggedErrorSuppressions { get; private set; }

        /// <inheritdoc />
        public bool LogError(Suppression suppression)
        {
            if (suppressionEngine.IsErrorSuppressed(suppression))
                return false;

            HasLoggedErrorSuppressions = true;
            LogError(suppression.DiagnosticId, suppression.Message);

            return true;
        }

        /// <inheritdoc />
        public bool LogWarning(Suppression suppression)
        {
            if (suppressionEngine.IsErrorSuppressed(suppression))
                return false;

            LogWarning(suppression.DiagnosticId, suppression.Message);

            return true;
        }
    }
}
