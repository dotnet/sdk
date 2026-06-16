// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal static class Logger
{
    [InterpolatedStringHandler]
    internal readonly struct LoggerInterpolatedStringHandler
    {
        /// <summary>The handler we use to perform the formatting.</summary>
        private readonly StringBuilder.AppendInterpolatedStringHandler _stringBuilderHandler;
        private readonly StringBuilder? _stringBuilder;

        public LoggerInterpolatedStringHandler(int literalLength, int formattedCount, out bool shouldAppend)
        {
            shouldAppend = Logger.TraceEnabled;
            if (shouldAppend)
            {
                _stringBuilder = new StringBuilder();
                _stringBuilderHandler = new(literalLength, formattedCount, _stringBuilder);
            }
        }

        public override string ToString()
            => _stringBuilder?.ToString() ?? string.Empty;

        public void AppendLiteral(string value) => _stringBuilderHandler!.AppendLiteral(value);

        public void AppendFormatted<T>(T value) => _stringBuilderHandler.AppendFormatted<T>(value);

        public void AppendFormatted<T>(T value, string? format) => _stringBuilderHandler.AppendFormatted<T>(value, format);

        public void AppendFormatted<T>(T value, int alignment) => _stringBuilderHandler.AppendFormatted<T>(value, alignment);

        public void AppendFormatted<T>(T value, int alignment, string? format) => _stringBuilderHandler.AppendFormatted<T>(value, alignment, format);

        public void AppendFormatted(ReadOnlySpan<char> value) => _stringBuilderHandler.AppendFormatted(value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => _stringBuilderHandler.AppendFormatted(value, alignment, format);

        public void AppendFormatted(string? value) => _stringBuilderHandler.AppendFormatted(value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => _stringBuilderHandler.AppendFormatted(value, alignment, format);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => _stringBuilderHandler.AppendFormatted(value, alignment, format);
    }

    public static bool TraceEnabled { get; private set; }
    private static readonly string? _traceFilePath;
    private static readonly object _lock = new();

    static Logger()
    {
        _traceFilePath = Environment.GetEnvironmentVariable(CliConstants.TestTraceLoggingEnvVar);
        TraceEnabled = !string.IsNullOrEmpty(_traceFilePath);

        string? directoryPath = Path.GetDirectoryName(_traceFilePath);
        if (!string.IsNullOrEmpty(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }

    /// <summary>
    /// Use this overload carefully for performance reasons.
    /// We don't want the argument to have expensive calculations.
    /// </summary>
    /// <param name="message"></param>
    public static void LogTrace(string message)
    {
        if (TraceEnabled)
        {
            LogTraceCore(message);
        }
    }

    public static void LogTrace(ref LoggerInterpolatedStringHandler handler)
    {
        if (TraceEnabled)
        {
            LogTraceCore(handler.ToString());
        }
    }

    public static void LogTrace<T>(T arg, Func<T, string> messageGetter)
    {
        if (TraceEnabled)
        {
            LogTraceCore(messageGetter(arg));
        }
    }

    private static void LogTraceCore(string message)
    {
        try
        {
            message = $"[dotnet test - {DateTimeOffset.UtcNow:MM/dd/yyyy HH:mm:ss.fff}]{message}";

            lock (_lock)
            {
                using StreamWriter logFile = File.AppendText(_traceFilePath!);
                logFile.WriteLine(message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[dotnet test - {DateTimeOffset.UtcNow:MM/dd/yyyy HH:mm:ss.fff}]{ex}");
        }
    }
}
