// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Spectre.Console;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Walkthrough;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class DotnetBotBannerTests
{
    /// <summary>
    /// Renders a panel to a plain-text string at the given terminal width.
    /// </summary>
    private static string RenderPanel(Panel panel, int width)
    {
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(writer),
            Ansi = AnsiSupport.No,
        });
        console.Profile.Width = width;
        console.Write(panel);
        return writer.ToString();
    }

    [Fact]
    public void Panel_ShowsTextContent()
    {
        var panel = DotnetBotBanner.BuildPanel();
        string output = RenderPanel(panel, 80);

        output.Should().Contain("dotnetup");
        output.Should().Contain(".NET installation manager for developers.");

        // Rounded box border characters
        output.Should().Contain("╭");
        output.Should().Contain("╰");
    }
}
