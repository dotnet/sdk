// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
[DoNotParallelize]
public class JsonProgressTargetTests
{
    private const string Version = "11.0.100-rc.2.26504.105";

    [TestMethod]
    public void InstallLifecycle_WritesVersionedEventsWithoutPadding()
    {
        string archivePath = Path.GetTempFileName();
        using var output = new StringWriter();
        try
        {
            File.WriteAllBytes(archivePath, new byte[1024 * 1024]);
            using (var reporter = new JsonProgressTarget(output).CreateProgressReporter())
            {
                var tracker = new ExtractorProgressTracker(reporter, InstallComponent.SDK, Version, versionDisplayWidth: Version.Length + 10);

                var (download, downloadTask) = tracker.BeginDownload();
                download.Should().NotBeNull();
                download!.Report(new DownloadProgress(512 * 1024, 1024 * 1024));
                download.Report(new DownloadProgress(1024 * 1024, 1024 * 1024));
                tracker.CompleteDownload(downloadTask, archivePath);

                var installTask = tracker.BeginExtraction();
                installTask.MaxValue = 4;
                for (int i = 0; i < 4; i++)
                {
                    installTask.Value += 1;
                }

                tracker.CompleteExtraction(installTask);
            }

            var events = ReadEvents(output);
            string sdk = InstallComponent.SDK.GetDisplayName();
            events.Select(e => (e.Event, e.Task, e.Description, e.Percent)).Should().Equal(
                ("started", 1, $"{ProgressFormatting.ActionDownloading} {sdk} {Version}", null),
                ("progress", 1, $"{ProgressFormatting.ActionDownloading} {sdk} {Version} (0.5 MB / 1.0 MB)", 50),
                ("finished", 1, $"{ProgressFormatting.ActionDownloaded} {sdk} {Version} (1.0 MB / 1.0 MB)", null),
                ("started", 2, $"{ProgressFormatting.ActionInstalling} {sdk} {Version}", null),
                ("progress", 2, $"{ProgressFormatting.ActionInstalling} {sdk} {Version}", 25),
                ("progress", 2, $"{ProgressFormatting.ActionInstalling} {sdk} {Version}", 50),
                ("progress", 2, $"{ProgressFormatting.ActionInstalling} {sdk} {Version}", 75),
                ("finished", 2, $"{ProgressFormatting.ActionInstalled} {sdk} {Version}", null));
            events.Should().OnlyContain(e => e.Version == JsonProgressTarget.EventVersion);
        }
        finally
        {
            File.Delete(archivePath);
        }
    }

    [TestMethod]
    public void TaskWithoutFinishedDescription_FinishesOnDisposeWithStartDescription()
    {
        using var output = new StringWriter();
        using (var reporter = new JsonProgressTarget(output).CreateProgressReporter())
        {
            reporter.AddTask("Downloading dotnetup", 100).Value = 100;
        }

        ReadEvents(output).Select(e => (e.Event, e.Description)).Should().Equal(
            ("started", "Downloading dotnetup"),
            ("finished", "Downloading dotnetup"));
    }

    [TestMethod]
    [DataRow("sdk install 10.0 --progress json", ProgressMode.Json)]
    [DataRow("sdk install 10.0 --progress json --no-progress", ProgressMode.None)]
    [DataRow("self update --progress json", ProgressMode.Json)]
    public void ResolveProgressMode_SelectsJsonOnlyWhenRequested(string commandLine, ProgressMode expected)
    {
        var result = Parser.Parse(commandLine.Split(' '));

        result.Errors.Should().BeEmpty();
        CommonOptions.ResolveProgressMode(result).Should().Be(expected);
    }

    [TestMethod]
    public void JsonProgressOutput_ReservesStdoutForEventsAndRestoresConsoles()
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        IAnsiConsole originalConsole = AnsiConsole.Console;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);

            using (JsonProgressOutput.Begin())
            {
                AnsiConsole.WriteLine("styled message");
                Console.WriteLine("plain message");
                JsonProgressOutput.Events.WriteLine("{}");
            }

            Console.WriteLine("after scope");
            AnsiConsole.Console.Should().BeSameAs(originalConsole);
            stdout.ToString().Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries).Should().Equal("{}", "after scope");
            stderr.ToString().Should().Contain("styled message").And.Contain("plain message");
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            AnsiConsole.Console = originalConsole;
        }
    }

    private static List<ProgressEvent> ReadEvents(StringWriter output) =>
        output.ToString().Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(line =>
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                return new ProgressEvent(
                    root.GetProperty("version").GetInt32(),
                    root.GetProperty("event").GetString()!,
                    root.GetProperty("task").GetInt32(),
                    root.GetProperty("description").GetString()!,
                    root.TryGetProperty("percent", out var percent) ? percent.GetInt32() : null);
            })
            .ToList();

    private sealed record ProgressEvent(int Version, string Event, int Task, string Description, int? Percent);
}
