// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Utilities;
using Microsoft.Extensions.Logging;

namespace Microsoft.TemplateEngine.Tasks.Utilities
{
    /// <summary>
    /// An <see cref="ILoggerProvider"/> that creates <see cref="ILogger"/>s which passes
    /// all the logs to MSBuild's <see cref="TaskLoggingHelper"/>.
    /// </summary>
    internal class MSBuildLoggerProvider : ILoggerProvider
    {
        private readonly TaskLoggingHelper _loggingHelper;

        public MSBuildLoggerProvider(TaskLoggingHelper loggingHelperToWrap)
        {
            _loggingHelper = loggingHelperToWrap;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new MSBuildLogger(categoryName, _loggingHelper);
        }

        public void Dispose() { }
    }
}
