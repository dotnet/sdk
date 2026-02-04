// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Utilities;

namespace Microsoft.Extensions.Logging.MSBuild;

/// <summary>
/// An <see cref="ILoggerProvider"/> that creates <see cref="ILogger"/>s which passes
/// all the logs to MSBuild's <see cref="TaskLoggingHelper"/>.
/// </summary>
public class MSBuildLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly TaskLoggingHelper _loggingHelper;
    private readonly Dictionary<string, MSBuildLogger> _loggers = [];
    private IExternalScopeProvider? _scopeProvider;

    public MSBuildLoggerProvider(TaskLoggingHelper loggingHelperToWrap)
    {
        _loggingHelper = loggingHelperToWrap;
    }

    public ILogger CreateLogger(string categoryName)
    {
        lock (_loggers)
        {
            if (!_loggers.TryGetValue(categoryName, out var logger))
            {
                logger = new MSBuildLogger(categoryName, _loggingHelper, _scopeProvider);
                _loggers[categoryName] = logger;
            }
            return logger;
        }
    }

    public void Dispose() { }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
        lock (_loggers)
        {
            foreach (var logger in _loggers.Values)
            {
                logger.SetScopeProvider(scopeProvider);
            }
        }
    }
}
