// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using NuGet.Common;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader
{
    internal class NuGetConsoleLogger : ILogger
    {
        public void Log(LogLevel level, string data)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    LogDebug(data);
                    break;
                case LogLevel.Error:
                    LogError(data);
                    break;
                case LogLevel.Information:
                    LogInformation(data);
                    break;
                case LogLevel.Minimal:
                    LogMinimal(data);
                    break;
                case LogLevel.Verbose:
                    LogVerbose(data);
                    break;
                case LogLevel.Warning:
                    LogWarning(data);
                    break;
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
            Reporter.Verbose.WriteLine($"[NuGet Manager] [DEBUG] {data}");
        }

        public void LogError(string data)
        {
            Reporter.Error.WriteLine($"[NuGet Manager] [Error] {data}");
        }

        public void LogInformation(string data)
        {
            Reporter.Output.WriteLine($"[NuGet Manager] [Info] {data}");
        }

        public void LogInformationSummary(string data)
        {
            Reporter.Output.WriteLine($"[NuGet Manager] [Info Summary] {data}");
        }

        public void LogMinimal(string data)
        {
            Reporter.Output.WriteLine($"[NuGet Manager] {data}");
        }

        public void LogVerbose(string data)
        {
            Reporter.Verbose.WriteLine($"[NuGet Manager] [Verbose] {data}");
        }

        public void LogWarning(string data)
        {
            Reporter.Error.WriteLine($"[NuGet Manager] [Warning] {data}");
        }
    }
}
