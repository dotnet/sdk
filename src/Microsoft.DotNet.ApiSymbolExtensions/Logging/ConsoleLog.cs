// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ApiSymbolExtensions.Logging
{
    /// <summary>
    /// Class to define common logging abstraction to the console across the APICompat and GenAPI codebases.
    /// </summary>
    public class ConsoleLog : ILog
    {
        private readonly MessageImportance _messageImportance;

        /// <inheritdoc />
        public bool HasLoggedErrors { get; private set; }

        public ConsoleLog(MessageImportance messageImportance) =>
            _messageImportance = messageImportance;

        /// <inheritdoc />
        public virtual void LogError(string format, params string[] args)
        {
            HasLoggedErrors = true;
            Console.Error.WriteLine(string.Format(format, args));
        }

        /// <inheritdoc />
        public virtual void LogError(string code, string format, params string[] args)
        {
            HasLoggedErrors = true;
            Console.Error.WriteLine($"{code}: {string.Format(format, args)}");
        }

        /// <inheritdoc />
        public virtual void LogWarning(string format, params string[] args) =>
            Console.WriteLine(string.Format(format, args));

        /// <inheritdoc />
        public virtual void LogWarning(string code, string format, string[] args) =>
            Console.WriteLine($"{code}: {string.Format(format, args)}");

        /// <inheritdoc />
        public virtual void LogMessage(string format, params string[] args) =>
            LogMessage(MessageImportance.Normal, format, args);

        /// <inheritdoc />
        public virtual void LogMessage(MessageImportance importance, string format, params string[] args)
        {
            if (importance > _messageImportance)
                return;

            Console.WriteLine(string.Format(format, args));
        }
    }
}
