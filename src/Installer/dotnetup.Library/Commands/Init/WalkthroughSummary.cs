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
    /// <param name="configuredAccessMode">The currently configured access mode, or null when unconfigured.</param>
    public static WalkthroughDecision Show(WalkthroughPlan plan, DotnetAccessMode? configuredAccessMode)
    {
        RenderSummaryBlock(plan, configuredAccessMode);

        bool isConfigured = configuredAccessMode is not null;
        IReadOnlyList<SummaryChoice> choices = BuildSummaryChoices(isConfigured);
        int defaultIndex = GetDefaultChoiceIndex(choices, isConfigured);

        int selected = InteractiveOptionSelector.Show(
            Strings.SummaryPrompt,
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
            ? Strings.SummaryProceedTitleConfigured
            : Strings.SummaryProceedTitleUnconfigured;
        string proceedDescription = isConfigured
            ? Strings.SummaryProceedDescriptionConfigured
            : Strings.SummaryProceedDescriptionUnconfigured;

        return
        [
            new SummaryChoice(
                new SelectableOption(proceedTitle, proceedDescription, string.Empty),
                WalkthroughDecision.Proceed),
            new SummaryChoice(
                new SelectableOption(Strings.SummaryCustomizeTitle, Strings.SummaryCustomizeDescription, string.Empty),
                WalkthroughDecision.Customize),
            new SummaryChoice(
                new SelectableOption(Strings.SummaryExitTitle, Strings.SummaryExitDescription, string.Empty),
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

    private static void RenderSummaryBlock(WalkthroughPlan plan, DotnetAccessMode? configuredAccessMode)
    {
        SpectreAnsiConsole.MarkupLine(string.Format(
            CultureInfo.InvariantCulture,
            Strings.SummaryWelcome,
            $"[{DotnetupTheme.Current.Brand} bold]dotnetup[/]"));
        SpectreAnsiConsole.WriteLine();

        RenderChannelLine(plan.ChannelDisplay);
        RenderModeLine(plan.AccessMode, configuredAccessMode);
        RenderMigrationSummary(plan.Migrations);

        SpectreAnsiConsole.WriteLine();
    }

    private static void RenderChannelLine(DefaultChannelDisplay channel)
    {
        if (channel.ChannelLabel is null)
        {
            return;
        }

        string label = Strings.SummaryChannelLabel.PadRight(LabelWidth);
        string value = DotnetupTheme.Brand(channel.ChannelLabel.EscapeMarkup());
        string suffix = channel.GlobalJsonPath is not null
            ? " " + DotnetupTheme.Dim("(" + string.Format(
                CultureInfo.InvariantCulture,
                Strings.SummaryChannelGlobalJsonSuffix,
                channel.GlobalJsonPath.EscapeMarkup()) + ")")
            : string.Empty;

        SpectreAnsiConsole.MarkupLine(label + value + suffix);
    }

    private static void RenderModeLine(
        DotnetAccessMode recommended,
        DotnetAccessMode? configuredAccessMode)
    {
        string label = Strings.SummaryModeLabel.PadRight(LabelWidth);
        string value = DotnetupTheme.Brand(DotnetAccessModeDisplay.GetNameWithSuggestedHint(recommended).EscapeMarkup());
        string current = configuredAccessMode is { } pref
            ? "  " + DotnetupTheme.Warning("(" + string.Format(
                CultureInfo.InvariantCulture,
                Strings.SummaryModeCurrent,
                DotnetAccessModeDisplay.GetName(pref).EscapeMarkup()) + ")")
            : string.Empty;

        SpectreAnsiConsole.MarkupLine(label + value + current);
    }

    private static void RenderMigrationSummary(List<MigrationWorkflow.MigrationSelection> migrations)
    {
        if (migrations.Count == 0)
        {
            return;
        }

        SpectreAnsiConsole.WriteLine();
        SpectreAnsiConsole.MarkupLine(Strings.SummaryMigrateHeader);

        var items = InitWorkflows.FormatMigrationDisplayItems(migrations);
        foreach (var item in items.Take(MigrationWorkflow.MigrationPreviewCount))
        {
            SpectreAnsiConsole.MarkupLine("  " + DotnetupTheme.Dim(Constants.Symbols.Bullet) + " " + item.EscapeMarkup());
        }

        int remaining = items.Count - MigrationWorkflow.MigrationPreviewCount;
        if (remaining > 0)
        {
            SpectreAnsiConsole.MarkupLine("  " + DotnetupTheme.Dim(
                string.Format(CultureInfo.InvariantCulture, Strings.SummaryMigrateMore, remaining)));
        }
    }
}
