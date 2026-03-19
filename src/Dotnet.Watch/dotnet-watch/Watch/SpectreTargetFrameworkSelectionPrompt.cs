// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Spectre.Console;

namespace Microsoft.DotNet.Watch;

internal sealed class SpectreTargetFrameworkSelectionPrompt(IAnsiConsole? console = null) : TargetFrameworkSelectionPrompt
{
    protected override Task<string> PromptAsync(IReadOnlyList<string> targetFrameworks, CancellationToken cancellationToken)
    {
        var prompt = new SelectionPrompt<string>()
            .Title($"[cyan]{Markup.Escape(Resources.SelectTargetFrameworkPrompt)}[/]")
            .PageSize(10)
            .MoreChoicesText($"[gray]({Markup.Escape(Resources.MoreFrameworksText)})[/]")
            .AddChoices(targetFrameworks)
            .EnableSearch()
            .SearchPlaceholderText(Resources.SearchPlaceholderText);

        return prompt.ShowAsync(console ?? AnsiConsole.Console, cancellationToken);
    }
}
