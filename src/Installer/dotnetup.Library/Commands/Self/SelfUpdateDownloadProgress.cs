// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Self;

/// <summary>Reports a self-update download using the shared installation progress targets.</summary>
internal static class SelfUpdateDownloadProgress
{
    public static void Run(bool noProgress, Action<IProgress<DownloadProgress>> download)
    {
        IProgressTarget target = noProgress ? new NonUpdatingProgressTarget() : new SpectreProgressTarget();
        using var reporter = new LazyProgressReporter(target);
        var description = Strings.SelfUpdateDownloading.EscapeMarkup();
        var task = reporter.AddTask(description, 100);
        download(new DownloadProgressReporter(task, description));
        task.Value = task.MaxValue;
    }
}