// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Writes plain progress lines for redirected or non-interactive output: one when a task starts, one when it
/// finishes, and, when <c>progressStepPercent</c> is positive, one each time a task crosses another step.
/// </summary>
public class NonUpdatingProgressTarget(int progressStepPercent = 0) : AppendOnlyProgressTarget(progressStepPercent)
{
    protected override void WriteStarted(int taskId, string description) =>
        AnsiConsole.WriteLine(description + "...");

    protected override void WriteStep(int taskId, string startDescription, string currentDescription, int percent)
    {
        string detail = currentDescription.StartsWith(startDescription, StringComparison.Ordinal) ? currentDescription[startDescription.Length..] : string.Empty;
        AnsiConsole.WriteLine(FormattableString.Invariant($"{startDescription}: {percent}%{detail}"));
    }

    protected override void WriteFinished(int taskId, string description, bool hasFinishedDescription)
    {
        if (hasFinishedDescription)
        {
            AnsiConsole.WriteLine(description);
        }
        else
        {
            AnsiConsole.MarkupLine($"{DotnetupTheme.Brand("Completed:")} {description}");
        }
    }
}
