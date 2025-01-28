// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

internal sealed class TestProgressState : ProgressStateBase
{
    public TestProgressState(long id, string assembly, string? targetFramework, string? architecture, IStopwatch stopwatch)
        : base(id, stopwatch)
    {
        Assembly = assembly;
        TargetFramework = targetFramework;
        Architecture = architecture;
        AssemblyName = Path.GetFileName(assembly)!;
    }

    public string Assembly { get; }

    public string AssemblyName { get; }

    public string? TargetFramework { get; }

    public string? Architecture { get; }

    public List<string> Attachments { get; } = new();

    public List<IProgressMessage> Messages { get; } = new();

    public int FailedTests { get; internal set; }

    public int PassedTests { get; internal set; }

    public int SkippedTests { get; internal set; }

    public int TotalTests { get; internal set; }

    public List<(string? DisplayName, string? UID)> DiscoveredTests { get; internal set; } = new();

    public int? ExitCode { get; internal set; }

    public bool Success { get; internal set; }

    internal void AddError(string text)
        => Messages.Add(new ErrorMessage(text));

    internal void AddWarning(string text)
        => Messages.Add(new WarningMessage(text));
}
