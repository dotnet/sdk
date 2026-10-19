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
    [DataRow(false)]
    [DataRow(true)]
    [ResourceLock(WellKnownResources.Console)]
    public void CancellationWarning_IsPreservedAboveProgress(bool interactive)
    {
        const string warning = "The .NET installer is still running and cannot be cancelled from dotnetup. Waiting for it to finish; no further installs will be started...";
        const string description = "Installing .NET SDK";
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        var originalConsole = AnsiConsole.Console;
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = interactive ? AnsiSupport.Yes : AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = interactive ? InteractionSupport.Yes : InteractionSupport.No,
            Out = new AnsiConsoleOutput(output),
        });
        console.Profile.Width = 200;
        bool cancellationRequested = false;
        try
        {
            AnsiConsole.Console = console;
            console.Progress().AutoRefresh(false).Columns(new TaskDescriptionColumn()).Start(context =>
            {
                var task = context.AddTask(description);
                context.Refresh();

                BundleProcessRunner.ReportCancellation(() => cancellationRequested = true);
                context.Refresh();

                string rendered = ConsoleOutputNormalizer.StripAnsi(output.ToString());
                rendered.Should().Contain(warning);
                if (interactive)
                {
                    rendered.Should().Contain(warning + Environment.NewLine + description,
                        "the live renderer must print the warning above, not inside, the progress region");
                }

                task.Value = task.MaxValue;
            });
            cancellationRequested.Should().BeTrue();
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }
}
