// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Tasks;

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

        public bool ReportProcessOutput
            => false;

        public void ProcessOutput(string projectPath, string data)
            => throw new InvalidOperationException();

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

        public void Report(MessageDescriptor descriptor, string prefix, object?[] args)
        {
            if (!descriptor.TryGetMessage(prefix, args, out var message))
            {
                return;
            }

            switch (descriptor.Severity)
            {
                case MessageSeverity.Error:
                    WriteLine(console.Error, message, ConsoleColor.Red, descriptor.Emoji);
                    break;

                case MessageSeverity.Warning:
                    WriteLine(console.Out, message, ConsoleColor.Yellow, descriptor.Emoji);
                    break;

                case MessageSeverity.Output:
                    if (!IsQuiet)
                    {
                        WriteLine(console.Out, message, color: null, descriptor.Emoji);
                    }
                    break;

                case MessageSeverity.Verbose:
                    if (IsVerbose)
                    {
                        WriteLine(console.Out, message, ConsoleColor.DarkGray, descriptor.Emoji);
                    }
                    break;
            }
        }
    }
}
