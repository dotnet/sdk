// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.HotReload;

internal readonly record struct LogEvent(EventId Id, LogLevel Level, string Message);

internal static class LogEvents
{
    // Non-shared event ids start at 0.
    private static int s_id = 1000;

    private static LogEvent Create(LogLevel level, string message)
        => new(new EventId(s_id++), level, message);

    public static void Log(this ILogger logger, LogEvent logEvent, params object[] args)
        => logger.Log(logEvent.Level, logEvent.Id, logEvent.Message, args);

    public static readonly LogEvent UpdatesApplied = Create(LogLevel.Debug, "Updates applied: {0} out of {1}.");
}
