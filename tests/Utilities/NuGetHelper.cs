// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Tools.Utilities;
using Xunit.Abstractions;

#nullable enable

namespace Microsoft.CodeAnalysis.Tools.Tests.Utilities
{
    internal static class NuGetHelper
    {
        public static async Task<int> PerformRestore(string workspaceFilePath, ITestOutputHelper output)
        {
            var workspacePath = Path.Combine(TestProjectsPathHelper.GetProjectsDirectory(), workspaceFilePath);

            var processInfo = ProcessRunner.CreateProcess("dotnet", $"restore \"{workspacePath}\"", captureOutput: true, displayWindow: false);
            var restoreResult = await processInfo.Result;

            output.WriteLine(string.Join(Environment.NewLine, restoreResult.OutputLines));

            return restoreResult.ExitCode;
        }
    }
}
