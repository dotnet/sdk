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
    private List<MSBuildLogger> _loggers = new List<MSBuildLogger>();
    private IExternalScopeProvider? _scopeProvider;

    public MSBuildLoggerProvider(TaskLoggingHelper loggingHelperToWrap)
    {
        _loggingHelper = loggingHelperToWrap;
    }

    public ILogger CreateLogger(string categoryName)
    {
        var logger = new MSBuildLogger(categoryName, _loggingHelper, _scopeProvider);
        _loggers.Add(logger);
        return logger;
    }

    public void Dispose() { }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
        foreach (var logger in _loggers)
        {
            logger.SetScopeProvider(scopeProvider);
        }
    }
}
