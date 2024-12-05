// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Diagnostics;
using Microsoft.Build.Graph;
using Microsoft.DotNet.Watcher;
using Microsoft.DotNet.Watcher.Internal;

namespace Microsoft.Extensions.Tools.Internal
{
    internal class TestReporter(ITestOutputHelper output) : IReporter
    {
        private readonly Dictionary<int, Action> _actions = [];
        public readonly List<string> ProcessOutput = [];

        public bool EnableProcessOutputReporting
            => true;

        public event Action<string, OutputLine>? OnProjectProcessOutput;
        public event Action<OutputLine>? OnProcessOutput;

        public void ReportProcessOutput(OutputLine line)
        {
            output.WriteLine(line.Content);
            ProcessOutput.Add(line.Content);

            OnProcessOutput?.Invoke(line);
        }

        public void ReportProcessOutput(ProjectGraphNode project, OutputLine line)
        {
            var content = $"[{project.GetDisplayName()}]: {line.Content}";

            output.WriteLine(content);
            ProcessOutput.Add(content);

            OnProjectProcessOutput?.Invoke(project.ProjectInstance.FullPath, line);
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
