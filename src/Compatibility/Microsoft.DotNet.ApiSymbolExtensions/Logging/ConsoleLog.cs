// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiSymbolExtensions.Logging
{
    /// <summary>
    /// Class to define common logging abstraction to the console across the APICompat and GenAPI codebases.
    /// </summary>
    public class ConsoleLog(MessageImportance messageImportance, string? noWarn = null) : ILog
    {
        private readonly HashSet<string> _noWarn = string.IsNullOrEmpty(noWarn) ? [] : new(noWarn!.Split(';'), StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc />
        public bool HasLoggedErrors { get; private set; }

        /// <inheritdoc />
        public virtual void LogError(string message)
        {
            HasLoggedErrors = true;
            Console.Error.WriteLine(message);
        }

        /// <inheritdoc />
        public virtual void LogError(string code, string message)
        {
            HasLoggedErrors = true;
            Console.Error.WriteLine($"{code}: {message}");
        }

        /// <inheritdoc />
        public virtual void LogWarning(string message) =>
            Console.WriteLine(message);

        /// <inheritdoc />
        public virtual void LogWarning(string code, string message)
        {
            string messageTextWithCode = $"{code}: {message}";

            // Mimic MSBuild which logs suppressed warnings as low importance messages.
            if (_noWarn.Contains(code))
            {
                LogMessage(MessageImportance.Low, messageTextWithCode);
            }
            else
            {
                Console.WriteLine(messageTextWithCode);
            }
        }

        /// <inheritdoc />
        public virtual void LogMessage(string message) =>
            LogMessage(MessageImportance.Normal, message);

        /// <inheritdoc />
        public virtual void LogMessage(MessageImportance importance, string message)
        {
            if (importance > messageImportance)
                return;

            Console.WriteLine(message);
        }
    }
}
