// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Spectre.Console;

namespace Microsoft.DotNet.Watch;

internal sealed class TargetFrameworkSelectionPrompt(string title, string moreChoicesText, string searchPlaceholderText, IAnsiConsole? console = null)
{
    public IReadOnlyList<string>? PreviousTargetFrameworks { get; set; }
    public string? PreviousSelection { get; set; }

    public async ValueTask<string> SelectAsync(IReadOnlyList<string> targetFrameworks, CancellationToken cancellationToken)
    {
        var orderedTargetFrameworks = targetFrameworks.Order(StringComparer.OrdinalIgnoreCase).ToArray();

        if (PreviousSelection != null && PreviousTargetFrameworks?.SequenceEqual(orderedTargetFrameworks, StringComparer.OrdinalIgnoreCase) == true)
        {
            return PreviousSelection;
        }

        PreviousTargetFrameworks = orderedTargetFrameworks;

        var prompt = new SelectionPrompt<string>()
            .Title($"[cyan]{Markup.Escape(title)}[/]")
            .PageSize(10)
            .MoreChoicesText($"[gray]({Markup.Escape(moreChoicesText)})[/]")
            .AddChoices(targetFrameworks)
            .EnableSearch()
            .SearchPlaceholderText(searchPlaceholderText);

        PreviousSelection = await prompt.ShowAsync(console ?? AnsiConsole.Console, cancellationToken);
        return PreviousSelection;
    }
}
