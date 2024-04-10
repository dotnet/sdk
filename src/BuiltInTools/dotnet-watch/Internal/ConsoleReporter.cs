// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.Tools.Internal
{
    /// <summary>
    /// This API supports infrastructure and is not intended to be used
    /// directly from your code. This API may change or be removed in future releases.
    /// </summary>
    internal sealed class ConsoleReporter(IConsole console, bool verbose, bool quiet, bool suppressEmojis) : IReporter
    {
        public bool IsVerbose { get; } = verbose;
        public bool IsQuiet { get; } = quiet;
        public bool SuppressEmojis { get; } = suppressEmojis;

        private readonly object _writeLock = new();

        private void WriteLine(TextWriter writer, string message, ConsoleColor? color, string emoji)
        {
            lock (_writeLock)
            {
                console.ForegroundColor = ConsoleColor.DarkGray;
                writer.Write($"dotnet watch {(SuppressEmojis ? ":" : emoji)} ");
                console.ResetColor();

                if (color.HasValue)
                {
                    console.ForegroundColor = color.Value;
                }

                writer.WriteLine(message);

                if (color.HasValue)
                {
                    console.ResetColor();
                }
            }
        }

        public void Error(string message, string emoji = "❌")
        {
            WriteLine(console.Error, message, ConsoleColor.Red, emoji);
        }

        public void Warn(string message, string emoji = "⌚")
        {
            WriteLine(console.Out, message, ConsoleColor.Yellow, emoji);
        }

        public void Output(string message, string emoji = "⌚")
        {
            if (IsQuiet)
            {
                return;
            }

            WriteLine(console.Out, message, color: null, emoji);
        }

        public void Verbose(string message, string emoji = "⌚")
        {
            if (!IsVerbose)
            {
                return;
            }

            WriteLine(console.Out, message, ConsoleColor.DarkGray, emoji);
        }
    }
}
