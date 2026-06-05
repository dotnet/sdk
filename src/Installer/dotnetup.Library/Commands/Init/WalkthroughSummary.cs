// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Spectre.Console;
using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;

/// <summary>
/// Renders the first-run / init summary block and the three-option selector that lets the
/// user proceed with the recommended defaults, customize the setup, or exit. The summary
/// only displays values that have already been resolved into a <see cref="WalkthroughPlan"/>;
/// it does not resolve or apply any setup itself.
/// </summary>
internal static class WalkthroughSummary
{
    private const int LabelWidth = 15;

    /// <summary>
    /// Renders the summary block and runs the selector, returning the user's decision.
    /// Assumes the banner has already been written by the caller.
    /// </summary>
    /// <param name="plan">The recommended setup to display.</param>
    /// <param name="configuredPreference">The currently configured path preference, or null when unconfigured.</param>
    public static WalkthroughDecision Show(WalkthroughPlan plan, PathPreference? configuredPreference)
    {
        RenderSummaryBlock(plan, configuredPreference);

        bool isConfigured = configuredPreference is not null;
        IReadOnlyList<SummaryChoice> choices = BuildSummaryChoices(isConfigured);
        int defaultIndex = GetDefaultChoiceIndex(choices, isConfigured);

        int selected = InteractiveOptionSelector.Show(
            "Would you like to install .NET with the recommended settings?",
            [.. choices.Select(choice => choice.Option)],
            defaultIndex);

        return choices[selected].Decision;
    }

    /// <summary>
    /// Builds the ordered selector choices, pairing each option with the decision it produces.
    /// The order is always [proceed/override, customize, exit]; only the first option's wording
    /// differs between configured and unconfigured.
    /// </summary>
    internal static IReadOnlyList<SummaryChoice> BuildSummaryChoices(bool isConfigured)
    {
        string proceedTitle = isConfigured
            ? "Yes, override settings with defaults"
            : "Yes, proceed with defaults and install";
        string proceedDescription = isConfigured
            ? "Replace your current settings with the recommended ones above."
            : "Install with the recommended settings shown above.";

        return
        [
            new SummaryChoice(
                new SelectableOption("y", proceedTitle, proceedDescription, string.Empty),
                WalkthroughDecision.Proceed),
            new SummaryChoice(
                new SelectableOption("c", "No, customize setup", "Choose the channel, mode, and migrations yourself.", string.Empty),
                WalkthroughDecision.Customize),
            new SummaryChoice(
                new SelectableOption("x", "Exit without changes", "Make no changes and quit.", string.Empty),
                WalkthroughDecision.Exit),
        ];
    }

    /// <summary>
    /// Returns the index of the choice to highlight by default. Unconfigured users are nudged toward
    /// proceeding; already-configured users default to customizing so they do not accidentally
    /// overwrite their saved settings.
    /// </summary>
    internal static int GetDefaultChoiceIndex(IReadOnlyList<SummaryChoice> choices, bool isConfigured)
    {
        WalkthroughDecision defaultDecision = isConfigured ? WalkthroughDecision.Customize : WalkthroughDecision.Proceed;
        for (int i = 0; i < choices.Count; i++)
        {
            if (choices[i].Decision == defaultDecision)
            {
                return i;
            }
        }

        return 0;
    }

    private static void RenderSummaryBlock(WalkthroughPlan plan, PathPreference? configuredPreference)
    {
        SpectreAnsiConsole.MarkupLine($"Welcome to [{DotnetupTheme.Current.Brand} bold]dotnetup[/]!");
        SpectreAnsiConsole.WriteLine();

        RenderChannelLine(plan.ChannelDisplay);
        RenderModeLine(plan.PathPreference, configuredPreference);
        RenderMigrationSummary(plan.Migrations);

        SpectreAnsiConsole.WriteLine();
    }

    private static void RenderChannelLine(DefaultChannelDisplay channel)
    {
        if (channel.ChannelLabel is null)
        {
            return;
        }

        string dim = DotnetupTheme.Current.Dim;
        string label = "SDK Channel:".PadRight(LabelWidth);
        string suffix = channel.GlobalJsonPath is not null
            ? string.Format(
                CultureInfo.InvariantCulture,
                " [{0}](determined from global.json at {1})[/]",
                dim,
                channel.GlobalJsonPath.EscapeMarkup())
            : string.Empty;

        SpectreAnsiConsole.MarkupLine(string.Format(
            CultureInfo.InvariantCulture,
            "{0}[{1}]{2}[/]{3}",
            label,
            DotnetupTheme.Current.Brand,
            channel.ChannelLabel.EscapeMarkup(),
            suffix));
    }

    private static void RenderModeLine(
        PathPreference recommended,
        PathPreference? configuredPreference)
    {
        string label = "Mode:".PadRight(LabelWidth);
        string recommendedName = PathPreferenceDisplay.GetName(recommended);

        string current = configuredPreference is { } pref
            ? string.Format(
                CultureInfo.InvariantCulture,
                "  [{0}](current: {1})[/]",
                DotnetupTheme.Current.Warning,
                PathPreferenceDisplay.GetNameWithoutHint(pref).EscapeMarkup())
            : string.Empty;

        SpectreAnsiConsole.MarkupLine(string.Format(
            CultureInfo.InvariantCulture,
            "{0}[{1}]{2}[/]{3}",
            label,
            DotnetupTheme.Current.Brand,
            recommendedName.EscapeMarkup(),
            current));
    }

    private static void RenderMigrationSummary(List<MigrationWorkflow.MigrationSelection> migrations)
    {
        if (migrations.Count == 0)
        {
            return;
        }

        string dim = DotnetupTheme.Current.Dim;
        SpectreAnsiConsole.WriteLine();
        SpectreAnsiConsole.MarkupLine("System installs to migrate:");

        var items = InitWorkflows.FormatMigrationDisplayItems(migrations);
        int shown = Math.Min(MigrationWorkflow.MigrationPreviewCount, items.Count);
        for (int i = 0; i < shown; i++)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(
                CultureInfo.InvariantCulture,
                "  [{0}]{1}[/] {2}",
                dim,
                Constants.Symbols.Bullet,
                items[i].EscapeMarkup()));
        }

        if (items.Count > shown)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(
                CultureInfo.InvariantCulture,
                "  [{0}]... and {1} more[/]",
                dim,
                items.Count - shown));
        }
    }
}
