// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.Build.Tasks;

namespace Microsoft.DotNet.ApiSymbolExtensions.Logging
{
    /// <summary>
    /// Class to define common logging abstraction for MSBuild tasks across the APICompat and GenAPI codebases.
    /// </summary>
    internal class MSBuildLog : ILog
    {
        internal readonly Logger _log;

        /// <inheritdoc />
        public bool HasLoggedErrors => _log.HasLoggedErrors;

        public MSBuildLog(Logger log) =>
            _log = log;

        /// <inheritdoc />
        public virtual void LogError(string format, params string[] args) =>
            LogCore(MessageLevel.Error, null, format, args);

        /// <inheritdoc />
        public virtual void LogError(string code, string format, params string[] args) =>
            LogCore(MessageLevel.Error, code, format, args);

        /// <inheritdoc />
        public virtual void LogWarning(string format, params string[] args) =>
            LogCore(MessageLevel.Warning, null, format, args);

        /// <inheritdoc />
        public virtual void LogWarning(string code, string format, string[] args) =>
            LogCore(MessageLevel.Warning, code, format, args);

        /// <inheritdoc />
        public virtual void LogMessage(string format, params string[] args) =>
            LogCore(MessageLevel.NormalImportance, null, format, args);

        /// <inheritdoc />
        public virtual void LogMessage(MessageImportance importance, string format, params string[] args) =>
            LogCore((MessageLevel)importance, null, format, args);

        private void LogCore(MessageLevel level, string? code, string format, params string[] args) =>
            _log.Log(new Message(level, string.Format(format, args), code));
    }
}
