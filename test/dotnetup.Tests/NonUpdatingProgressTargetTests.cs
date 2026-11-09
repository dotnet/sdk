// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
[DoNotParallelize]
public class NonUpdatingProgressTargetTests
{
    private const string Version = "11.0.100-rc.2.26504.105";

    [TestMethod]
    public void InstallLifecycle_WritesUnpaddedStartAndFinishedLines()
    {
        string archivePath = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(archivePath, new byte[1024 * 1024]);

            string[] lines = CaptureLines(() =>
            {
                using var reporter = new NonUpdatingProgressTarget().CreateProgressReporter();
                var tracker = new ExtractorProgressTracker(reporter, InstallComponent.SDK, Version, versionDisplayWidth: Version.Length + 10);

                var (_, downloadTask) = tracker.BeginDownload();
                tracker.CompleteDownload(downloadTask, archivePath);

                var installTask = tracker.BeginExtraction();
                installTask.Value = 50;
                installTask.Value = 100;
                tracker.CompleteExtraction(installTask);
            });

            string sdk = InstallComponent.SDK.GetDisplayName();
            lines.Should().Equal(
                $"{ProgressFormatting.ActionDownloading} {sdk} {Version}...",
                $"{ProgressFormatting.ActionDownloaded} {sdk} {Version} (1.0 MB / 1.0 MB)",
                $"{ProgressFormatting.ActionInstalling} {sdk} {Version}...",
                $"{ProgressFormatting.ActionInstalled} {sdk} {Version}");
        }
        finally
        {
            File.Delete(archivePath);
        }
    }

    [TestMethod]
    public void TaskWithoutFinishedLabel_ReportsCompletedOnceOnDispose()
    {
        string[] lines = CaptureLines(() =>
        {
            using var reporter = new NonUpdatingProgressTarget().CreateProgressReporter();
            var task = reporter.AddTask("Downloading dotnetup", 100);
            var progress = new DownloadProgressReporter(task, "Downloading dotnetup");
            progress.Report(new DownloadProgress(1024 * 1024, 1024 * 1024));
            task.Value = task.MaxValue;
        });

        lines.Should().Equal("Downloading dotnetup...", "Completed: Downloading dotnetup");
    }

    [TestMethod]
    public void IncompleteTask_DoesNotReportCompletion()
    {
        string[] lines = CaptureLines(() =>
        {
            using var reporter = new NonUpdatingProgressTarget().CreateProgressReporter();
            var task = reporter.AddTask("Installing  .NET SDK", 100);
            task.Value = 40;
            task.Description = "Installed .NET SDK";
        });

        lines.Should().Equal("Installing .NET SDK...");
    }

    private static string[] CaptureLines(Action action)
    {
        IAnsiConsole previousConsole = AnsiConsole.Console;
        using var output = new StringWriter();
        try
        {
            AnsiConsole.Console = AdminModeTestEnvironment.CreateConsole(output);
            action();
        }
        finally
        {
            AnsiConsole.Console = previousConsole;
        }

        return output.ToString().Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
    }
}
