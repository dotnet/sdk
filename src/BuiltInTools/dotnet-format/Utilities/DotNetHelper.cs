// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Utilities
{
    internal static class DotNetHelper
    {
        public static async Task<int> PerformRestoreAsync(string workspaceFilePath, ILogger logger)
        {
            var processInfo = ProcessRunner.CreateProcess("dotnet", $"restore \"{workspaceFilePath}\"", captureOutput: true, displayWindow: false);
            var restoreResult = await processInfo.Result;

            logger.LogDebug(string.Join(Environment.NewLine, restoreResult.OutputLines));

            return restoreResult.ExitCode;
        }
    }
}
