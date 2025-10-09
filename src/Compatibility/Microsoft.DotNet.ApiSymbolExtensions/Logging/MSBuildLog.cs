// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Tasks;

namespace Microsoft.DotNet.ApiSymbolExtensions.Logging
{
    /// <summary>
    /// Class to define common logging abstraction for MSBuild tasks across the APICompat and GenAPI codebases.
    /// </summary>
    internal class MSBuildLog(Logger log) : ILog
    {
        /// <inheritdoc />
        public bool HasLoggedErrors => log.HasLoggedErrors;

        /// <inheritdoc />
        public virtual void LogError(string message) =>
            LogCore(MessageLevel.Error, null, message);

        /// <inheritdoc />
        public virtual void LogError(string code, string message) =>
            LogCore(MessageLevel.Error, code, message);

        /// <inheritdoc />
        public virtual void LogWarning(string message) =>
            LogCore(MessageLevel.Warning, null, message);

        /// <inheritdoc />
        public virtual void LogWarning(string code, string message) =>
            LogCore(MessageLevel.Warning, code, message);

        /// <inheritdoc />
        public virtual void LogMessage(string message) =>
            LogCore(MessageLevel.NormalImportance, null, message);

        /// <inheritdoc />
        public virtual void LogMessage(MessageImportance importance, string message) =>
            LogCore((MessageLevel)importance, null, message);

        private void LogCore(MessageLevel level, string? code, string message) =>
            log.Log(new Message(level, message, code));
    }
}
