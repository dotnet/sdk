// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SpectreTestConsole = Spectre.Console.Testing.TestConsole;

namespace Microsoft.DotNet.Watch.UnitTests;

public class TargetFrameworkSelectionPromptTests
{
    [Theory]
    [CombinatorialData]
    public async Task SelectsFrameworkByArrowKeysAndEnter([CombinatorialRange(0, count: 3)] int index)
    {
        var console = new SpectreTestConsole();
        console.Profile.Capabilities.Interactive = true;

        // Press DownArrow 'index' times to move to the desired item, then Enter to select
        for (var i = 0; i < index; i++)
        {
            console.Input.PushKey(ConsoleKey.DownArrow);
        }
        console.Input.PushKey(ConsoleKey.Enter);

        var frameworks = new[] { "net7.0", "net8.0", "net9.0" };
        var prompt = new SpectreTargetFrameworkSelectionPrompt(console);

        var result = await prompt.SelectAsync(frameworks, CancellationToken.None);
        Assert.Equal(frameworks[index], result);
        Assert.Equal(frameworks[index], prompt.PreviousSelection);
    }

    [Theory]
    [CombinatorialData]
    public async Task PreviousSelectionIsReusedWhenFrameworksUnchanged([CombinatorialRange(0, count: 3)] int index)
    {
        var console = new SpectreTestConsole();
        console.Profile.Capabilities.Interactive = true;

        // First selection via key presses
        for (var i = 0; i < index; i++)
        {
            console.Input.PushKey(ConsoleKey.DownArrow);
        }
        console.Input.PushKey(ConsoleKey.Enter);

        var frameworks = new[] { "net7.0", "net8.0", "net9.0" };
        var prompt = new SpectreTargetFrameworkSelectionPrompt(console);

        var first = await prompt.SelectAsync(frameworks, CancellationToken.None);
        Assert.Equal(frameworks[index], first);

        // Same frameworks (reordered, different casing) should reuse previous selection without prompting
        var second = await prompt.SelectAsync(["NET9.0", "net7.0", "net8.0"], CancellationToken.None);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task PromptsAgainWhenFrameworksChange()
    {
        var console = new SpectreTestConsole();
        console.Profile.Capabilities.Interactive = true;

        // First selection: pick first item
        console.Input.PushKey(ConsoleKey.Enter);
        // Second selection (after frameworks change): pick second item
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);

        var prompt = new SpectreTargetFrameworkSelectionPrompt(console);

        var first = await prompt.SelectAsync(["net7.0", "net8.0", "net9.0"], CancellationToken.None);
        Assert.Equal("net7.0", first);

        // Different set of frameworks — should prompt again
        var second = await prompt.SelectAsync(["net9.0", "net10.0"], CancellationToken.None);
        Assert.Equal("net10.0", second);
    }

    [Fact]
    public async Task SelectsFrameworkBySearchText()
    {
        var console = new SpectreTestConsole();
        console.Profile.Capabilities.Interactive = true;

        // Type "net9.0" to filter, then Enter to select the match
        console.Input.PushText("net9.0");
        console.Input.PushKey(ConsoleKey.Enter);

        var frameworks = new[] { "net7.0", "net8.0", "net9.0", "net10.0" };
        var prompt = new SpectreTargetFrameworkSelectionPrompt(console);

        var result = await prompt.SelectAsync(frameworks, CancellationToken.None);
        Assert.Equal("net9.0", result);
    }
}
