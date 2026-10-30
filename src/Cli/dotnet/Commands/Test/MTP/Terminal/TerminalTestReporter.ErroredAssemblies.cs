// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Copyright (c) Microsoft Corporation. All rights reserved.
// Adapted from https://github.com/microsoft/testfx/blob/f4d8d35bf45ce0ece75a66e1b197c96684767e48/src/Platform/Microsoft.Testing.Platform/OutputDevice/Terminal/TerminalTestReporter.ErroredAssemblies.cs

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

internal sealed partial class TerminalTestReporter
{
    private readonly Lock _erroredAssembliesLock = new();
    private readonly List<ErroredAssemblyRecord> _erroredAssemblies = [];

    private void RecordErroredAssembly(TestProgressState assemblyRun, int exitCode, string? outputData, string? errorData)
    {
        lock (_erroredAssembliesLock)
        {
            _erroredAssemblies.Add(new ErroredAssemblyRecord(
                assemblyRun.Assembly, assemblyRun.TargetFramework, assemblyRun.Architecture,
                exitCode, outputData, errorData));
        }
    }

    private void AppendErroredAssemblyRecap(ITerminal terminal)
    {
        ErroredAssemblyRecord[] errored;
        lock (_erroredAssembliesLock)
        {
            if (_erroredAssemblies.Count == 0)
            {
                return;
            }

            errored = [.. _erroredAssemblies];
        }

        terminal.AppendLine();
        terminal.SetColor(TerminalColor.DarkRed);
        terminal.AppendLine(CliCommandStrings.ErroredAssembliesHeader);
        terminal.ResetColor();
        foreach (ErroredAssemblyRecord failure in errored)
        {
            terminal.Append(SingleIndentation);
            AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, failure.AssemblyPath, failure.TargetFramework, failure.Architecture);
            terminal.AppendLine();
            AppendExecutableSummary(terminal, failure.ExitCode, failure.OutputData, failure.ErrorData);
        }
    }

    private readonly record struct ErroredAssemblyRecord(
        string AssemblyPath,
        string? TargetFramework,
        string? Architecture,
        int ExitCode,
        string? OutputData,
        string? ErrorData);
}
