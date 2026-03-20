// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

public class TargetFrameworkSelectionPromptTests(ITestOutputHelper output)
{
    [Theory]
    [CombinatorialData]
    public async Task PreviousSelection([CombinatorialRange(0, count: 3)] int index)
    {
        var console = new TestConsole(output);
        var consoleInput = new ConsoleInputReader(console, LogLevel.Debug, suppressEmojis: false);
        var prompt = new TargetFrameworkSelectionPrompt(consoleInput);

        var frameworks = new[] { "net7.0", "net8.0", "net9.0" };
        var expectedTfm = frameworks[index];

        // first selection:
        console.QueuedKeyPresses.Add(new ConsoleKeyInfo((char)('1' + index), ConsoleKey.D1 + index, shift: false, alt: false, control: false));

        Assert.Equal(expectedTfm, await prompt.SelectAsync(frameworks, CancellationToken.None));
        Assert.Equal(expectedTfm, prompt.PreviousSelection);
        console.QueuedKeyPresses.Clear();

        // should use previous selection:
        Assert.Equal(expectedTfm, await prompt.SelectAsync(["NET9.0", "net7.0", "net8.0"], CancellationToken.None));

        // second selection:
        console.QueuedKeyPresses.Add(new ConsoleKeyInfo('3', ConsoleKey.D3, shift: false, alt: false, control: false));

        // should prompt again:
        Assert.Equal("net10.0", await prompt.SelectAsync(["net9.0", "net7.0", "net10.0"], CancellationToken.None));
    }
}
