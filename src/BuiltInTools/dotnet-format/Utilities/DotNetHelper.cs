// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
