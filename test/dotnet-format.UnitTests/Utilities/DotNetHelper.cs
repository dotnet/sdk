// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.Tools.Utilities;

namespace Microsoft.CodeAnalysis.Tools.Tests.Utilities
{
    internal static partial class DotNetHelper
    {
        public static async Task<int> NewProjectAsync(string templateName, string outputPath, string languageName, ITestOutputHelper output)
        {
            var language = languageName switch
            {
                LanguageNames.CSharp => "C#",
                LanguageNames.VisualBasic => "VB",
                LanguageNames.FSharp => "F#",
                _ => throw new ArgumentOutOfRangeException(nameof(languageName), actualValue: languageName, message: "Only C#, F# and VB.NET project are supported.")
            };

            var processInfo = ProcessRunner.CreateProcess("dotnet", $"new \"{templateName}\" -o \"{outputPath}\" --language \"{language}\"", captureOutput: true, displayWindow: false);
            var restoreResult = await processInfo.Result;

            output.WriteLine(string.Join(Environment.NewLine, restoreResult.OutputLines));

            return restoreResult.ExitCode;
        }

        public static async Task<int> PerformBuildAsync(string workspaceFilePath, ITestOutputHelper output)
        {
            var workspacePath = Path.IsPathRooted(workspaceFilePath)
                ? workspaceFilePath
                : Path.Combine(TestProjectsPathHelper.GetProjectsDirectory(), workspaceFilePath);

            var processInfo = ProcessRunner.CreateProcess("dotnet", $"build \"{workspacePath}\"", captureOutput: true, displayWindow: false);
            var restoreResult = await processInfo.Result;

            output.WriteLine(string.Join(Environment.NewLine, restoreResult.OutputLines));

            return restoreResult.ExitCode;
        }

        public static async Task<int> PerformRestoreAsync(string workspaceFilePath, ITestOutputHelper output)
        {
            var workspacePath = Path.IsPathRooted(workspaceFilePath)
                ? workspaceFilePath
                : Path.Combine(TestProjectsPathHelper.GetProjectsDirectory(), workspaceFilePath);

            var processInfo = ProcessRunner.CreateProcess("dotnet", $"restore \"{workspacePath}\"", captureOutput: true, displayWindow: false);
            var restoreResult = await processInfo.Result;

            output.WriteLine(string.Join(Environment.NewLine, restoreResult.OutputLines));

            return restoreResult.ExitCode;
        }
    }
}
