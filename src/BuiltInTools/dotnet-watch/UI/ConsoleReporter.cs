// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch
{
    /// <summary>
    /// This API supports infrastructure and is not intended to be used
    /// directly from your code. This API may change or be removed in future releases.
    /// </summary>
    internal sealed class ConsoleReporter(IConsole console, bool verbose, bool quiet, bool suppressEmojis) : IReporter, IProcessOutputReporter
    {
        public bool IsVerbose { get; } = verbose;
        public bool IsQuiet { get; } = quiet;
        public bool SuppressEmojis { get; } = suppressEmojis;

        private readonly Lock _writeLock = new();

        bool IProcessOutputReporter.PrefixProcessOutput
            => false;

        void IProcessOutputReporter.ReportOutput(OutputLine line)
        {
            lock (_writeLock)
            {
                (line.IsError ? console.Error : console.Out).WriteLine(line.Content);
            }
        }

        private void WriteLine(TextWriter writer, string message, ConsoleColor? color, Emoji emoji)
        {
            lock (_writeLock)
            {
                console.ForegroundColor = ConsoleColor.DarkGray;
                writer.Write((SuppressEmojis ? Emoji.Default : emoji).GetLogMessagePrefix());
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

        public void Report(EventId id, Emoji emoji, MessageSeverity severity, string message)
        {
            switch (severity)
            {
                case MessageSeverity.Error:
                    // Use stdout for error messages to preserve ordering with respect to other output.
                    WriteLine(console.Error, message, ConsoleColor.Red, emoji);
                    break;

                case MessageSeverity.Warning:
                    WriteLine(console.Error, message, ConsoleColor.Yellow, emoji);
                    break;

                case MessageSeverity.Output:
                    if (!IsQuiet)
                    {
                        WriteLine(console.Error, message, color: null, emoji);
                    }
                    break;

                case MessageSeverity.Verbose:
                    if (IsVerbose)
                    {
                        WriteLine(console.Error, message, ConsoleColor.DarkGray, emoji);
                    }
                    break;
            }
        }
    }
}
