// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Tools.Test;

internal static class Logger
{
    public static bool TraceEnabled { get; private set; }
    private static readonly string _traceFilePath;
    private static readonly object _lock = new();

    static Logger()
    {
        _traceFilePath = Environment.GetEnvironmentVariable(CliConstants.TestTraceLoggingEnvVar);
        TraceEnabled = !string.IsNullOrEmpty(_traceFilePath);

        string directoryPath = Path.GetDirectoryName(_traceFilePath);
        if (!string.IsNullOrEmpty(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }

    public static void LogTrace(Func<string> messageLog)
    {
        if (!TraceEnabled)
        {
            return;
        }

        try
        {
            string message = $"[dotnet test - {DateTimeOffset.UtcNow:MM/dd/yyyy HH:mm:ss.fff}]{messageLog()}";

            lock (_lock)
            {
                using StreamWriter logFile = File.AppendText(_traceFilePath);
                logFile.WriteLine(message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[dotnet test - {DateTimeOffset.UtcNow:MM/dd/yyyy HH:mm:ss.fff}]{ex}");
        }
    }
}
