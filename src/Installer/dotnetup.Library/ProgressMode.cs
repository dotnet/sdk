// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>How install and download progress is displayed.</summary>
public enum ProgressMode
{
    /// <summary>Use <see cref="Bars"/> on a terminal and <see cref="Lines"/> when output is redirected or in CI.</summary>
    Auto,

    /// <summary>Live progress bars that redraw in place.</summary>
    Bars,

    /// <summary>Plain text lines, including periodic percentage updates, for logs and pipes.</summary>
    Lines,

    /// <summary>One line when each task starts and one when it finishes.</summary>
    None,
}

internal static class ProgressTargets
{
    /// <summary>Percentage step between <see cref="ProgressMode.Lines"/> updates for one task.</summary>
    internal const int LinesProgressStepPercent = 25;

    public static IProgressTarget Create(ProgressMode mode) => mode switch
    {
        ProgressMode.None => new NonUpdatingProgressTarget(),
        ProgressMode.Lines => new NonUpdatingProgressTarget(LinesProgressStepPercent),
        _ => new SpectreProgressTarget(),
    };
}
