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

    [TestMethod]
    public void LinesMode_ReportsEachStepOnceWithCurrentDetail()
    {
        string[] lines = CaptureLines(() =>
        {
            using var reporter = ProgressTargets.Create(ProgressMode.Lines).CreateProgressReporter();
            var task = reporter.AddTask("Downloading  .NET SDK  " + Version, 100);
            task.RequiresKnownMaximum.Should().BeTrue();
            var progress = new DownloadProgressReporter(task, "Downloading  .NET SDK  " + Version);
            long total = 4 * 1024 * 1024;
            foreach (long bytes in new long[] { 512 * 1024, 1024 * 1024, 1536 * 1024, 2 * 1024 * 1024, 3 * 1024 * 1024, total })
            {
                progress.Report(new DownloadProgress(bytes, total));
            }

            task.Description = $"Downloaded .NET SDK {Version} (4.0 MB / 4.0 MB)";
        });

        lines.Should().Equal(
            $"Downloading .NET SDK {Version}...",
            $"Downloading .NET SDK {Version}: 25% (1.0 MB / 4.0 MB)",
            $"Downloading .NET SDK {Version}: 50% (2.0 MB / 4.0 MB)",
            $"Downloading .NET SDK {Version}: 75% (3.0 MB / 4.0 MB)",
            $"Downloaded .NET SDK {Version} (4.0 MB / 4.0 MB)");
    }

    [TestMethod]
    public void NoneMode_DoesNotRequestKnownMaximum()
    {
        using var reporter = ProgressTargets.Create(ProgressMode.None).CreateProgressReporter();
        CaptureLines(() => reporter.AddTask("Installing .NET SDK", 100).RequiresKnownMaximum.Should().BeFalse());
    }

    [TestMethod]
    [DataRow("sdk install 10.0 --no-progress", ProgressMode.None)]
    [DataRow("sdk install 10.0 --no-progress --progress bars", ProgressMode.None)]
    [DataRow("sdk install 10.0 --no-progress false --progress lines", ProgressMode.Lines)]
    [DataRow("sdk install 10.0 --progress bars", ProgressMode.Bars)]
    [DataRow("sdk install 10.0 --progress lines", ProgressMode.Lines)]
    [DataRow("sdk install 10.0 --progress none", ProgressMode.None)]
    [DataRow("self update --progress lines", ProgressMode.Lines)]
    [DataRow("update --progress none", ProgressMode.None)]
    public void ResolveProgressMode_HonorsExplicitOptions(string commandLine, ProgressMode expected)
    {
        var result = Parser.Parse(commandLine.Split(' '));

        result.Errors.Should().BeEmpty();
        CommonOptions.ResolveProgressMode(result).Should().Be(expected);
    }

    [TestMethod]
    [DataRow("sdk install 10.0")]
    [DataRow("sdk install 10.0 --progress auto")]
    public void ResolveProgressMode_AutoPicksBarsOrLinesFromTheConsole(string commandLine)
    {
        ProgressMode resolved = CommonOptions.ResolveProgressMode(Parser.Parse(commandLine.Split(' ')));

        if (Console.IsOutputRedirected)
        {
            resolved.Should().Be(ProgressMode.Lines);
        }
        else
        {
            resolved.Should().BeOneOf(ProgressMode.Bars, ProgressMode.Lines);
        }
    }

    [TestMethod]
    public void ProgressOption_RejectsUnknownMode()
    {
        Parser.Parse(["sdk", "install", "10.0", "--progress", "fancy"]).Errors.Should().NotBeEmpty();
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
