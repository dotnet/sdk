// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests
{
    internal class TestReporter(ITestOutputHelper output) : IReporter, IProcessOutputReporter
    {
        private readonly Dictionary<EventId, Action> _actions = [];
        public readonly List<string> ProcessOutput = [];
        public readonly List<(MessageSeverity severity, string text)> Messages = [];

        public bool IsVerbose
            => true;

        bool IProcessOutputReporter.PrefixProcessOutput
            => true;

        public event Action<OutputLine>? OnProcessOutput;

        void IProcessOutputReporter.ReportOutput(OutputLine line)
        {
            WriteTestOutput(line.Content);
            ProcessOutput.Add(line.Content);

            OnProcessOutput?.Invoke(line);
        }

        public SemaphoreSlim RegisterSemaphore(MessageDescriptor descriptor)
        {
            var semaphore = new SemaphoreSlim(initialCount: 0);
            RegisterAction(descriptor, () => semaphore.Release());
            return semaphore;
        }

        public void RegisterAction(MessageDescriptor eventId, Action action)
            => RegisterAction(eventId.Id, action);

        public void RegisterAction(EventId eventId, Action action)
        {
            if (_actions.TryGetValue(eventId, out var existing))
            {
                existing += action;
            }
            else
            {
                existing = action;
            }

            _actions[eventId] = existing;
        }

        public void Report(MessageDescriptor descriptor, string prefix, object?[] args)
        {
            if (descriptor.Severity != MessageSeverity.None)
            {
                var message = descriptor.GetMessage(prefix, args);
                Messages.Add((descriptor.Severity, message));
                WriteTestOutput($"{ToString(descriptor.Severity)} {descriptor.Emoji.ToDisplay()} {message}");
            }

            if (_actions.TryGetValue(descriptor.Id, out var action))
            {
                action();
            }
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

        private static string ToString(MessageSeverity severity)
            => severity switch
            {
                MessageSeverity.Verbose => "verbose",
                MessageSeverity.Output => "output",
                MessageSeverity.Warning => "warning",
                MessageSeverity.Error => "error",
                _ => throw new InvalidOperationException()
            };
    }
}
