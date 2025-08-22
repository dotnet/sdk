﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal static class LoggingUtilities
{
    public static ILogger CreateLogger(this ILoggerFactory factory, string componentName, string displayName)
        => factory.CreateLogger($"{componentName}|{displayName}");

    public static (string comonentName, string? displayName) ParseCategoryName(string categoryName)
        => categoryName.IndexOf('|') is int index && index > 0
            ? (categoryName[..index], categoryName[(index + 1)..])
            : (categoryName, null);

    public static string GetPrefix(Emoji emoji)
        => $"dotnet watch {emoji.ToDisplay()} ";
}
