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
        var (options, defaultIndex) = BuildSummaryOptions(isConfigured);

        int selected = InteractiveOptionSelector.Show(
            "Would you like to install .NET with the recommended settings?",
            options,
            defaultIndex);

        return EvaluateSummaryDecision(selected);
    }

    /// <summary>
    /// Builds the selector options and the default highlighted index for the summary.
    /// The option order is always [proceed/override, customize, exit]; only the first
    /// option's wording and the default index differ between configured and unconfigured.
    /// </summary>
    internal static (IReadOnlyList<SelectableOption> Options, int DefaultIndex) BuildSummaryOptions(bool isConfigured)
    {
        string proceedTitle = isConfigured
            ? "Yes, override settings with defaults"
            : "Yes, proceed with defaults and install";
        string proceedDescription = isConfigured
            ? "Replace your current settings with the recommended ones above."
            : "Install with the recommended settings shown above.";

        var options = new List<SelectableOption>
        {
            new("y", proceedTitle, proceedDescription, string.Empty),
            new("c", "No, customize setup", "Choose the channel, mode, and migrations yourself.", string.Empty),
            new("x", "Exit without changes", "Make no changes and quit.", string.Empty),
        };

        // Unconfigured users are nudged toward proceeding; already-configured users default
        // to customizing so they do not accidentally overwrite their saved settings.
        var defaultIndex = isConfigured ? 1 : 0;
        return (options, defaultIndex);
    }

    /// <summary>
    /// Maps the selected option index to a <see cref="WalkthroughDecision"/>.
    /// </summary>
    internal static WalkthroughDecision EvaluateSummaryDecision(int selectedIndex) => selectedIndex switch
    {
        0 => WalkthroughDecision.Proceed,
        1 => WalkthroughDecision.Customize,
        _ => WalkthroughDecision.Exit,
    };

    private static void RenderSummaryBlock(WalkthroughPlan plan, PathPreference? configuredPreference)
    {
        string brand = DotnetupTheme.Current.Brand;
        string dim = DotnetupTheme.Current.Dim;
        string configured = DotnetupTheme.Current.Warning;

        SpectreAnsiConsole.MarkupLine($"Welcome to [{brand} bold]dotnetup[/]!");
        SpectreAnsiConsole.WriteLine();

        RenderChannelLine(plan.ChannelDisplay, brand, dim);
        RenderModeLine(plan.PathPreference, configuredPreference, brand, configured);
        RenderMigrationSummary(plan.Migrations, dim);

        SpectreAnsiConsole.WriteLine();
    }

    private static void RenderChannelLine(DefaultChannelDisplay channel, string brand, string dim)
    {
        if (channel.ChannelLabel is null)
        {
            return;
        }

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
            brand,
            channel.ChannelLabel.EscapeMarkup(),
            suffix));
    }

    private static void RenderModeLine(
        PathPreference recommended,
        PathPreference? configuredPreference,
        string brand,
        string configured)
    {
        string label = "Mode:".PadRight(LabelWidth);
        string recommendedName = PathPreferenceDisplay.GetName(recommended);

        string current = configuredPreference is { } pref
            ? string.Format(
                CultureInfo.InvariantCulture,
                "  [{0}](current: {1})[/]",
                configured,
                PathPreferenceDisplay.GetName(pref).EscapeMarkup())
            : string.Empty;

        SpectreAnsiConsole.MarkupLine(string.Format(
            CultureInfo.InvariantCulture,
            "{0}[{1}]{2}[/]{3}",
            label,
            brand,
            recommendedName.EscapeMarkup(),
            current));
    }

    private static void RenderMigrationSummary(List<MigrationWorkflow.MigrationSelection> migrations, string dim)
    {
        if (migrations.Count == 0)
        {
            return;
        }

        SpectreAnsiConsole.WriteLine();
        SpectreAnsiConsole.MarkupLine("System installs to migrate:");

        var items = InitWorkflows.FormatMigrationDisplayItems(migrations);
        int shown = Math.Min(3, items.Count);
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
