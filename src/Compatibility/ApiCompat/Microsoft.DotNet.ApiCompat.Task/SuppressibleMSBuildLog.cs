﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;

namespace Microsoft.DotNet.ApiCompat.Task
{
    /// <summary>
    /// Class that can log Suppressions in an MSBuild task, by implementing MSBuildLog and ISuppressibleLog.
    /// </summary>
    internal sealed class SuppressibleMSBuildLog(NET.Build.Tasks.Logger log,
        ISuppressionEngine suppressionEngine) : MSBuildLog(log), ISuppressibleLog
    {
        /// <inheritdoc />
        public bool HasLoggedErrorSuppressions { get; private set; }

        /// <inheritdoc />
        public bool LogError(Suppression suppression, string code, string message)
        {
            if (suppressionEngine.IsErrorSuppressed(suppression))
                return false;

            HasLoggedErrorSuppressions = true;
            LogError(code, message);

            return true;
        }

        /// <inheritdoc />
        public bool LogWarning(Suppression suppression, string code, string message)
        {
            if (suppressionEngine.IsErrorSuppressed(suppression))
                return false;

            LogWarning(code, message);

            return true;
        }
    }
}
