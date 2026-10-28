// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Utilities
{
    internal static class DotNetHelper
    {
        public static Task<int> PerformRestoreAsync(string workspaceFilePath, ILogger logger)
            => ProcessRunner.RunAsync(
                "dotnet",
                ["restore", workspaceFilePath],
                logger.IsEnabled(LogLevel.Debug) ? output => logger.LogDebug(output) : null);
    }
}
