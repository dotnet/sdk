// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Diagnostics;

namespace Microsoft.DotNet.Watch.UnitTests
{
    internal class TestReporter(ITestOutputHelper output) : IReporter
    {
        private readonly Dictionary<int, Action> _actions = [];
        public readonly List<string> ProcessOutput = [];
        public readonly List<(MessageSeverity severity, string text)> Messages = [];

        public bool IsVerbose
            => true;

        public bool PrefixProcessOutput
            => true;

        public event Action<OutputLine>? OnProcessOutput;

        public void ReportProcessOutput(OutputLine line)
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

        public void RegisterAction(MessageDescriptor descriptor, Action action)
        {
            Debug.Assert(descriptor.Id != null);

            if (_actions.TryGetValue(descriptor.Id.Value, out var existing))
            {
                existing += action;
            }
            else
            {
                existing = action;
            }

            _actions[descriptor.Id.Value] = existing;
        }

        public void Report(MessageDescriptor descriptor, string prefix, object?[] args)
        {
            if (descriptor.TryGetMessage(prefix, args, out var message))
            {
                Messages.Add((descriptor.Severity, message));

                WriteTestOutput($"{ToString(descriptor.Severity)} {descriptor.Emoji} {message}");
            }

            if (descriptor.Id.HasValue && _actions.TryGetValue(descriptor.Id.Value, out var action))
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
