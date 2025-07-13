// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        if (!_loggers.TryGetValue(categoryName, out var logger))
        {
            logger = new MSBuildLogger(categoryName, _loggingHelper, _scopeProvider);
            lock (_loggers)
            {
                _loggers[categoryName] = logger;
            }
        }
        return logger;
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
