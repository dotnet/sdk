// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Common;
using INuGetLogger = global::NuGet.Common.ILogger;
using NuGetLogLevel = global::NuGet.Common.LogLevel;

namespace Microsoft.TemplateEngine.Edge.Installers.NuGet
{
    /// <summary>
    /// Default logger to be used with NuGet API. It forwards all the messages to different methods of ITemplateEngineHost depending on the log level.
    /// </summary>
    internal class NuGetLogger : INuGetLogger
    {
        private readonly Microsoft.Extensions.Logging.ILogger _baseLogger;

        internal NuGetLogger(Microsoft.Extensions.Logging.ILoggerFactory loggerFactory)
        {
            _baseLogger = loggerFactory.CreateLogger("NuGetLogger") ?? throw new System.ArgumentNullException(nameof(loggerFactory));
        }

        public void Log(NuGetLogLevel level, string data)
        {
            switch (level)
            {
                case NuGetLogLevel.Debug: LogDebug(data); break;
                case NuGetLogLevel.Error: LogError(data); break;
                case NuGetLogLevel.Information: LogInformation(data); break;
                case NuGetLogLevel.Minimal: LogMinimal(data); break;
                case NuGetLogLevel.Verbose: LogVerbose(data); break;
                case NuGetLogLevel.Warning: LogWarning(data); break;
            }
        }

        public void Log(ILogMessage message)
        {
            Log(message.Level, message.Message);
        }

        public Task LogAsync(NuGetLogLevel level, string data)
        {
            Log(level, data);
            return Task.FromResult(0);
        }

        public Task LogAsync(ILogMessage message)
        {
            Log(message);
            return Task.FromResult(0);
        }

        public void LogDebug(string data)
        {
            _baseLogger.LogDebug(data);
        }

        public void LogError(string data)
        {
            _baseLogger.LogError(data);
        }

        public void LogInformation(string data)
        {
            //TODO: NuGet is putting too much logs to info level, check if we want this data
            _baseLogger.LogDebug(data);
        }

        public void LogInformationSummary(string data)
        {
            _baseLogger.LogInformation(data);
        }

        public void LogMinimal(string data)
        {
            _baseLogger.LogInformation(data);
        }

        public void LogVerbose(string data)
        {
            _baseLogger.LogDebug(data);
        }

        public void LogWarning(string data)
        {
            _baseLogger.LogWarning(data);
        }
    }
}
