// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch;

internal sealed class ProcessSpec
{
    public required string Executable { get; init; }
    public string? WorkingDirectory { get; set; }
    public Dictionary<string, string> EnvironmentVariables { get; } = [];
    public IReadOnlyList<string> Arguments { get; set; } = [];
    public Action<OutputLine>? OnOutput { get; set; }
    public ProcessExitAction? OnExit { get; set; }
    public CancellationToken CancelOutputCapture { get; set; }
    public bool UseShellExecute { get; set; }

    /// <summary>
    /// True if the process is a user application, false if it is a helper process (e.g. dotnet build).
    /// </summary>
    public bool IsUserApplication { get; set; }

    public string? ShortDisplayName()
        => Path.GetFileNameWithoutExtension(Executable);

    public string GetArgumentsDisplay()
        => CommandLineUtilities.JoinArguments(Arguments ?? []);

    /// <summary>
    /// Stream output lines to the process output reporter when
    /// - output observer is installed so that the output is also streamd to the console;
    /// - testing to synchonize the output of the process with the logger output, so that the printed lines don't interleave;
    /// unless the caller has already redirected the output (e.g. for Aspire child processes).
    ///
    /// Do not redirect output otherwise as it disables the ability of the process to use Console APIs.
    /// </summary>
    public void RedirectOutput(Action<OutputLine>? outputObserver, IProcessOutputReporter outputReporter, EnvironmentOptions environmentOptions, string projectDisplayName)
    {
        if (environmentOptions.RunningAsTest || outputObserver != null)
        {
            OnOutput ??= line =>
            {
                outputReporter.ReportOutput(outputReporter.PrefixProcessOutput ? line with { Content = $"[{projectDisplayName}] {line.Content}" } : line);
            };

            OnOutput += outputObserver;
        }
    }
}
