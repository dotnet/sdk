// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Extensions.Logging;

using ILogger = Microsoft.Extensions.Logging.ILogger;
using IMSBuildLogger = Microsoft.Build.Framework.ILogger;

namespace Microsoft.DotNet.Watch;

internal sealed class BuildManager(ILogger logger, GlobalOptions options, EnvironmentOptions environmentOptions)
{
    /// <summary>
    /// Semaphore that ensures we only start one build build at a time per process, which is required by MSBuild.
    /// </summary>
    private static readonly SemaphoreSlim s_buildSemaphore = new(initialCount: 1);

    public ILogger Logger => logger;
    public EnvironmentOptions EnvironmentOptions => environmentOptions;

    public async ValueTask<Loggers> StartBuildAsync(string projectPath, string operationName, CancellationToken cancellationToken)
    {
        await s_buildSemaphore.WaitAsync(cancellationToken);
        return new(logger, environmentOptions.GetBinLogPath(projectPath, operationName, options));
    }

    public void ReportWatchedFiles(Dictionary<string, FileItem> fileItems)
    {
        logger.Log(MessageDescriptor.WatchingFilesForChanges, fileItems.Count);

        if (logger.IsEnabled(LogLevel.Trace))
        {
            foreach (var file in fileItems.Values)
            {
                logger.Log(MessageDescriptor.WatchingFilesForChanges_FilePath, file.StaticWebAssetRelativeUrl != null
                    ? $"{file.FilePath}{Path.PathSeparator}{string.Join(Path.PathSeparator, file.StaticWebAssetRelativeUrl)}"
                    : $"{file.FilePath}");
            }
        }
    }

    public sealed class Loggers(ILogger logger, string? binLogPath) : IEnumerable<IMSBuildLogger>, IDisposable
    {
        private readonly BinaryLogger? _binaryLogger = binLogPath != null
            ? new()
            {
                Verbosity = LoggerVerbosity.Diagnostic,
                Parameters = "LogFile=" + binLogPath,
            }
            : null;

        private readonly OutputLogger _outputLogger =
            new(logger)
            {
                Verbosity = LoggerVerbosity.Minimal
            };

        public void Dispose()
        {
            s_buildSemaphore.Release();
            _outputLogger.Clear();
        }

        public IEnumerator<IMSBuildLogger> GetEnumerator()
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
                logger.LogDebug("Binary log: '{BinLogPath}'", binLogPath);
            }

            _outputLogger.ReportOutput();
        }

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
    }

    private sealed class OutputLogger : ConsoleLogger
    {
        private readonly ILogger _logger;
        private readonly List<OutputLine> _messages = [];

        public OutputLogger(ILogger logger)
        {
            WriteHandler = Write;
            _logger = logger;
        }

        public IReadOnlyList<OutputLine> Messages
            => _messages;

        public void Clear()
            => _messages.Clear();

        private void Write(string message)
            => _messages.Add(new OutputLine(message.TrimEnd('\r', '\n'), IsError: false));

        public void ReportOutput()
        {
            _logger.LogInformation("MSBuild output:");
            BuildOutput.ReportBuildOutput(_logger, Messages, success: false);
        }
    }
}
