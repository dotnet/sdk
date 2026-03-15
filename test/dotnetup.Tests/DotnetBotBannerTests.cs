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
        });
        console.Profile.Width = width;
        console.Write(panel);
        return writer.ToString();
    }

    [Fact]
    public void WideTerminal_ShowsBotArt()
    {
        var panel = DotnetBotBanner.BuildPanel(80);
        string output = RenderPanel(panel, 80);

        // Bot art characters should be present
        output.Should().Contain("▄█▄");
        output.Should().Contain("▀█▀");
        output.Should().Contain("••");
        output.Should().Contain("■");

        // Text content
        output.Should().Contain("dotnetup");
        output.Should().Contain(".NET installation manager for developers.");

        // Rounded box border characters
        output.Should().Contain("╭");
        output.Should().Contain("╰");
    }

    [Fact]
    public void NarrowTerminal_ShowsTextOnly()
    {
        var panel = DotnetBotBanner.BuildPanel(40);
        string output = RenderPanel(panel, 40);

        // Text content should still be present
        output.Should().Contain("dotnetup");
        output.Should().Contain(".NET installation manager");

        // Bot art should NOT be present
        output.Should().NotContain("▄█▄");
        output.Should().NotContain("▀█▀");
        output.Should().NotContain("■");

        // Border should still be there
        output.Should().Contain("╭");
        output.Should().Contain("╰");
    }

    [Fact]
    public void Panel_IsSizedToContent()
    {
        var panel = DotnetBotBanner.BuildPanel(100);
        string output = RenderPanel(panel, 100);

        // Panel should auto-size to content (not expand to terminal width).
        string firstLine = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)[0];
        firstLine.Length.Should().BeLessThan(100);
        firstLine.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ExactThreshold_ShowsBotArt()
    {
        // At exactly MinWidthForBotArt (55), should still show bot art.
        var panel = DotnetBotBanner.BuildPanel(55);
        string output = RenderPanel(panel, 55);

        output.Should().Contain("▄█▄");
        output.Should().Contain("dotnetup");
    }

    [Fact]
    public void JustBelowThreshold_ShowsTextOnly()
    {
        // At 54 (one below threshold), should fall back to text-only.
        var panel = DotnetBotBanner.BuildPanel(54);
        string output = RenderPanel(panel, 54);

        output.Should().NotContain("▄█▄");
        output.Should().Contain("dotnetup");
    }

    [Fact]
    public void WideTerminal_AllBorderLinesAligned()
    {
        var panel = DotnetBotBanner.BuildPanel(80);
        string output = RenderPanel(panel, 80);

        string[] lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCountGreaterThan(2);

        // All lines in the panel should have the same width (expanded to terminal).
        int expectedWidth = lines[0].Length;
        foreach (string line in lines)
        {
            line.Length.Should().Be(expectedWidth, because: $"all panel lines should align, but \"{line}\" has length {line.Length}");
        }
    }
}
