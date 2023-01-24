// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ApiSymbolExtensions.Logging
{
    /// <summary>
    /// Interface to define common logging abstraction between Console and MSBuild tasks across the APICompat and GenAPI codebases.
    /// </summary>
    public interface ILog
    {
        /// <summary>
        /// True if any errors are logged.
        /// </summary>
        bool HasLoggedErrors { get; }

        /// <summary>
        /// Log an error based on a passed in format and additional arguments.
        /// <param name="format">The message format/param>
        /// <param name="args">The message format arguments</param>
        /// </summary>
        void LogError(string format, params string[] args);

        /// <summary>
        /// Log an error based on a passed in error code, format and additional arguments.
        /// <param name="code">The error code</param>
        /// <param name="format">The message format/param>
        /// <param name="args">The message format arguments</param>
        /// </summary>
        void LogError(string code, string format, params string[] args);

        /// <summary>
        /// Log a warning based on a passed in format and additional arguments.
        /// <param name="format">The message format</param>
        /// <param name="args">The message format arguments</param>
        /// </summary>
        void LogWarning(string format, params string[] args);

        /// <summary>
        /// Log a warning based on a passed in warning code, format and additional arguments.
        /// <param name="code">The warning code</param>
        /// <param name="format">The message format/param>
        /// <param name="args">The message format arguments</param>
        /// </summary>
        void LogWarning(string code, string format, string[] args);

        /// <summary>
        /// Log a message with normal importance based on a passed in format and additional arguments.
        /// <param name="format">The message format</param>
        /// <param name="args">The message format arguments</param>
        /// </summary>
        void LogMessage(string format, params string[] args);

        /// <summary>
        /// Log a message based on a passed in importance, format and additional arguments.
        /// <param name="importance">The message importance</param>
        /// <param name="format">The message format</param>
        /// <param name="args">The message format arguments</param>
        /// </summary>
        void LogMessage(MessageImportance importance, string format, params string[] args);
    }
}
