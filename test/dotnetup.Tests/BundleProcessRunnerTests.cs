// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.DotNet.Tools.Bootstrapper.WindowsInstaller;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Microsoft.NET.TestFramework;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class BundleProcessRunnerTests
{
    [TestMethod]
    [DataRow(false, 80, false)]
    [DataRow(false, 80, true)]
    [DataRow(true, 40, false)]
    [DataRow(true, 40, true)]
    [DataRow(true, 80, false)]
    [DataRow(true, 80, true)]
    [DataRow(true, 160, false)]
    [DataRow(true, 160, true)]
    [ResourceLock(WellKnownResources.Console)]
    public void CancellationWarning_IsPreservedAboveProgress(bool interactive, int width, bool azurePipelines)
    {
        const string warning = "Cannot cancel this installer. Waiting; skipping remaining installs.";
        const string description = "Installing .NET SDK";
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        var originalConsole = AnsiConsole.Console;
        var environmentVariables = new Dictionary<string, string>();
        if (azurePipelines)
        {
            environmentVariables["TF_BUILD"] = "True";
        }
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = interactive ? AnsiSupport.Yes : AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = interactive ? InteractionSupport.Yes : InteractionSupport.No,
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
            EnvironmentVariables = environmentVariables,
            Out = new AnsiConsoleOutput(output),
        });
        console.Profile.Width = width;
        console.Profile.Capabilities.Interactive.Should().Be(interactive);
        console.Profile.Capabilities.Ansi.Should().Be(interactive);
        bool cancellationRequested = false;
        try
        {
            AnsiConsole.Console = console;
            console.Progress().AutoRefresh(false).Columns(new TaskDescriptionColumn(), new ProgressBarColumn()).Start(context =>
            {
                var downloaded = context.AddTask("Downloaded .NET SDK");
                downloaded.Value = downloaded.MaxValue;
                var task = context.AddTask(description);
                context.Refresh();
                int noticeStart = output.GetStringBuilder().Length;

                BundleProcessRunner.ReportCancellation(() => cancellationRequested = true);

                string notice = ConsoleOutputNormalizer.StripAnsi(output.ToString()[noticeStart..]);
                if (interactive)
                {
                    AssertNoticeClearsProgressRows(notice, warning, width);
                }
                else
                {
                    notice.Should().Be(warning + Environment.NewLine);
                }

                context.Refresh();
                task.Value = task.MaxValue;
            });
            cancellationRequested.Should().BeTrue();
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }

    private static void AssertNoticeClearsProgressRows(string rendered, string warning, int width)
    {
        string[] noticeLines = rendered.Split(Environment.NewLine)
            .TakeWhile(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.TrimStart('\r'))
            .ToArray();

        string.Join(" ", noticeLines.Select(line => line.Trim())).Should().Be(warning);
        noticeLines.Should().OnlyContain(line => line.Length == width,
            "each notice line must overwrite the entire previous progress row");
        if (width >= 80)
        {
            noticeLines.Should().ContainSingle("the warning should fit a standard terminal without wrapping");
        }
    }
}
