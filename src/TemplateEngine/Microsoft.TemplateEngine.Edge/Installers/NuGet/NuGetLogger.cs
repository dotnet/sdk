// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using NuGet.Common;

namespace Microsoft.TemplateEngine.Edge.Installers.NuGet
{
    /// <summary>
    /// Default logger to be used with NuGet API. It forwards all the messages to different methods of ITemplateEngineHost depending on the log level.
    /// </summary>
    internal class NuGetLogger : ILogger
    {
        private const string DebugLogCategory = "Installer";
        private ITemplateEngineHost _host;
        internal NuGetLogger (IEngineEnvironmentSettings settings)
        {
            _host = settings.Host;
        }

        public void Log(LogLevel level, string data)
        {
            switch (level)
            {
                case LogLevel.Debug: LogDebug(data); break;
                case LogLevel.Error: LogError(data); break;
                case LogLevel.Information: LogInformation(data); break;
                case LogLevel.Minimal: LogMinimal(data); break;
                case LogLevel.Verbose: LogVerbose(data); break;
                case LogLevel.Warning: LogWarning(data); break;
            }
        }

        public void Log(ILogMessage message)
        {
            Log(message.Level, message.Message);
        }

        public Task LogAsync(LogLevel level, string data)
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
            _host.LogDiagnosticMessage(data, DebugLogCategory);
        }

        public void LogError(string data)
        {
            _host.OnCriticalError(null, data, null, 0);
        }

        public void LogInformation(string data)
        {
            //TODO: NuGet is putting too much logs to info level, check if we want this data
            _host.LogDiagnosticMessage(data, DebugLogCategory);
        }

        public void LogInformationSummary(string data)
        {
            _host.LogMessage(data);
        }

        public void LogMinimal(string data)
        {
            _host.LogMessage(data);
        }

        public void LogVerbose(string data)
        {
            _host.LogDiagnosticMessage(data, DebugLogCategory);
        }

        public void LogWarning(string data)
        {
            _host.OnNonCriticalError(null, data, null, 0);
        }
    }
}
