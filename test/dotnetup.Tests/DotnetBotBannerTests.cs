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

    [Fact]
    public void Panel_DoesNotContainBotArt()
    {
        var panel = DotnetBotBanner.BuildPanel();
        string output = RenderPanel(panel, 80);

        output.Should().NotContain("▄█▄");
        output.Should().NotContain("▀█▀");
        output.Should().NotContain("■");
    }

    [Fact]
    public void Panel_IsSizedToContent()
    {
        var panel = DotnetBotBanner.BuildPanel();
        string output = RenderPanel(panel, 100);

        // Panel should auto-size to content (not expand to terminal width).
        string firstLine = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)[0];
        firstLine.Length.Should().BeLessThan(100);
        firstLine.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Panel_AllBorderLinesAligned()
    {
        var panel = DotnetBotBanner.BuildPanel();
        string output = RenderPanel(panel, 80);

        string[] lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCountGreaterThan(2);

        char[] leftBorders = ['╭', '│', '╰'];
        char[] rightBorders = ['╮', '│', '╯'];

        foreach (string line in lines)
        {
            line.Should().NotBeEmpty();
            leftBorders.Should().Contain(line[0],
                because: "each line should start with a left border character");
            rightBorders.Should().Contain(line[^1],
                because: "each line should end with a right border character");
        }
    }
}
