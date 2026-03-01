// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Utilities
{
    internal static class DotNetHelper
    {
        internal static string BuildRestoreArguments(string workspaceFilePath, string? targetFramework)
        {
            var frameworkArg = targetFramework is not null ? $" -p:TargetFramework={targetFramework}" : string.Empty;
            return $"restore \"{workspaceFilePath}\"{frameworkArg}";
        }

        public static async Task<int> PerformRestoreAsync(string workspaceFilePath, ILogger logger, string? targetFramework = null)
        {
            var processInfo = ProcessRunner.CreateProcess("dotnet", BuildRestoreArguments(workspaceFilePath, targetFramework), captureOutput: true, displayWindow: false);
            var restoreResult = await processInfo.Result;

            logger.LogDebug(string.Join(Environment.NewLine, restoreResult.OutputLines));

            return restoreResult.ExitCode;
        }
    }
}
