// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests
{
    internal class TestReporter(ITestOutputHelper output) : IReporter, IProcessOutputReporter
    {
        public readonly List<string> ProcessOutput = [];
        public readonly List<(LogLevel level, string text)> Messages = [];

        bool IProcessOutputReporter.PrefixProcessOutput
            => true;

        public event Action<OutputLine>? OnProcessOutput;

        void IProcessOutputReporter.ReportOutput(OutputLine line)
        {
            WriteTestOutput(line.Content);
            ProcessOutput.Add(line.Content);

            OnProcessOutput?.Invoke(line);
        }

        public void Report(EventId id, Emoji emoji, LogLevel level, string message)
        {
            Messages.Add((level, message));
            WriteTestOutput($"{ToString(level)} {emoji.ToDisplay()} {message}");
        }

        private void WriteTestOutput(string line)
        {
            try
            {
                output.WriteLine(line);
            }
            catch (InvalidOperationException)
            {
                // May happen when a test is aborted and no longer running.
            }
        }

        private static string ToString(LogLevel level)
            => level switch
            {
                LogLevel.Trace or LogLevel.Debug => "verbose",
                LogLevel.Information => "output",
                LogLevel.Warning => "warning",
                LogLevel.Critical or LogLevel.Error => "error",
                _ => throw new InvalidOperationException()
            };
    }
}
