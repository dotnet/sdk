// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Diagnostics;

namespace Microsoft.Extensions.Tools.Internal
{
    internal class TestReporter(ITestOutputHelper output) : IReporter
    {
        private readonly Dictionary<int, Action> _actions = [];

        public bool ReportProcessOutput
            => true;

        public event Action<string, string>? OnProcessOutput;

        public void ProcessOutput(string projectPath, string data)
        {
            output.WriteLine($"[{Path.GetFileName(projectPath)}]: {data}");
            OnProcessOutput?.Invoke(projectPath, data);
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
                output.WriteLine($"{ToString(descriptor.Severity)} {descriptor.Emoji} {message}");
            }

            if (descriptor.Id.HasValue && _actions.TryGetValue(descriptor.Id.Value, out var action))
            {
                action();
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
