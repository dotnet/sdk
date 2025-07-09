// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace Microsoft.DotNet.Watch;

internal sealed class BuildReporter(IReporter reporter, EnvironmentOptions environmentOptions)
{
    public IReporter Reporter => reporter;
    public EnvironmentOptions EnvironmentOptions => environmentOptions;

    public Loggers GetLoggers(string projectPath, string operationName)
        => new(reporter, environmentOptions.GetTestBinLogPath(projectPath, operationName));

    public void ReportWatchedFiles(Dictionary<string, FileItem> fileItems)
    {
        reporter.Verbose($"Watching {fileItems.Count} file(s) for changes");

        if (environmentOptions.TestFlags.HasFlag(TestFlags.RunningAsTest))
        {
            foreach (var file in fileItems.Values)
            {
                reporter.Verbose(file.StaticWebAssetPath != null
                    ? $"> {file.FilePath}{Path.PathSeparator}{file.StaticWebAssetPath}"
                    : $"> {file.FilePath}");
            }
        }
    }

    public sealed class Loggers(IReporter reporter, string? binLogPath) : IEnumerable<ILogger>, IDisposable
    {
        private readonly BinaryLogger? _binaryLogger = binLogPath != null
            ? new()
            {
                Verbosity = LoggerVerbosity.Diagnostic,
                Parameters = "LogFile=" + binLogPath,
            }
            : null;

        private readonly OutputLogger _outputLogger =
            new(reporter)
            {
                Verbosity = LoggerVerbosity.Minimal
            };

        public void Dispose()
        {
            _outputLogger.Clear();
        }

        public IEnumerator<ILogger> GetEnumerator()
        {
            yield return _outputLogger;

            if (_binaryLogger != null)
            {
                yield return _binaryLogger;
            }
        }

        public void ReportOutput()
        {
            if (binLogPath != null)
            {
                reporter.Verbose($"Binary log: '{binLogPath}'");
            }

            _outputLogger.ReportOutput();
        }

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
    }

    private sealed class OutputLogger : ConsoleLogger
    {
        private readonly IReporter _reporter;
        private readonly List<OutputLine> _messages = [];

        public OutputLogger(IReporter reporter)
        {
            WriteHandler = Write;
            _reporter = reporter;
        }

        public IReadOnlyList<OutputLine> Messages
            => _messages;

        public void Clear()
            => _messages.Clear();

        private void Write(string message)
            => _messages.Add(new OutputLine(message.TrimEnd('\r', '\n'), IsError: false));

        public void ReportOutput()
        {
            _reporter.Output($"MSBuild output:");
            BuildOutput.ReportBuildOutput(_reporter, Messages, success: false, projectDisplay: null);
        }
    }
}
