// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init.Form;

/// <summary>
/// Builds the init form's fields from a resolved <see cref="WalkthroughPlan"/> and computes the
/// per-field detail shown beside them, and translates the user's accepted selections back into the
/// domain values (channel, access mode, whether to migrate). This is the single place that knows
/// the field/value ↔ domain mapping; the renderer and state machine stay domain-agnostic.
///
/// Construction is side-effect-free and network-free: choices come from the plan and static channel
/// tokens (concrete SDK versions are resolved later, only when an install actually runs), so simply
/// showing the form never triggers a download.
/// </summary>
internal sealed class InitFormModel
{
    // Maximum number of versions listed per migration component line before collapsing to "and N more".
    private const int MaxVersionsShown = 3;

    // Index of the "Yes" choice in the access-affecting yes/no fields (Migrate).
    private const int YesIndex = 0;

    private readonly FormField _channelField;
    private readonly IReadOnlyList<string?> _channelTokens;
    private readonly int _globalJsonChannelIndex;
    private readonly string? _globalJsonPath;

    private readonly FormField _accessModeField;
    private readonly IReadOnlyList<DotnetAccessMode> _accessModes;

    private readonly FormField? _migrateField;
    private readonly IReadOnlyList<MigrationWorkflow.MigrationSelection> _migrations;

    private readonly string _installPath;

    private InitFormModel(
        IReadOnlyList<FormField> fields,
        FormField channelField,
        IReadOnlyList<string?> channelTokens,
        int globalJsonChannelIndex,
        string? globalJsonPath,
        FormField accessModeField,
        IReadOnlyList<DotnetAccessMode> accessModes,
        FormField? migrateField,
        IReadOnlyList<MigrationWorkflow.MigrationSelection> migrations,
        string installPath)
    {
        Fields = fields;
        _channelField = channelField;
        _channelTokens = channelTokens;
        _globalJsonChannelIndex = globalJsonChannelIndex;
        _globalJsonPath = globalJsonPath;
        _accessModeField = accessModeField;
        _accessModes = accessModes;
        _migrateField = migrateField;
        _migrations = migrations;
        _installPath = installPath;
    }

    /// <summary>Short line under the banner.</summary>
    public string Subtitle { get; } = "Welcome to dotnetup!";

    /// <summary>The prompt shown above the fields.</summary>
    public string Question { get; } = "Install .NET with these settings?";

    /// <summary>The form fields, in display order.</summary>
    public IReadOnlyList<FormField> Fields { get; }

    /// <summary>True when the user changed the channel away from the recommended default.</summary>
    public bool ChannelChangedFromDefault => _channelField.IsChangedFromDefault;

    /// <summary>
    /// The channel the user chose: a fixed channel token, the typed custom value, or <c>null</c>
    /// when the user chose to skip the initial install ("none").
    /// </summary>
    public string? SelectedChannel()
    {
        if (_channelField.Selected.IsCustomInput)
        {
            return _channelField.CustomValue;
        }

        string? token = _channelTokens[_channelField.SelectedIndex];
        return string.Equals(token, InitWorkflows.NoneChannel, StringComparison.Ordinal) ? null : token;
    }

    /// <summary>The access mode the user chose.</summary>
    public DotnetAccessMode SelectedAccessMode() => _accessModes[_accessModeField.SelectedIndex];

    /// <summary>Whether the user chose to migrate existing system installs.</summary>
    public bool MigrateSelected() => _migrateField is not null && _migrateField.SelectedIndex == YesIndex;

    /// <summary>
    /// Computes the detail (help text + derived info lines) for the given field's value at
    /// <paramref name="choiceIndex"/>. Used both while browsing (the selected value) and while
    /// editing (the highlighted value).
    /// </summary>
    public FieldDetail BuildDetail(FormField field, int choiceIndex)
    {
        string helper = field.Choices[choiceIndex].HelperText;
        var lines = new List<DetailLine>();

        if (ReferenceEquals(field, _channelField))
        {
            if (choiceIndex == _globalJsonChannelIndex && _globalJsonPath is not null)
            {
                lines.Add(new DetailLine("From global.json:", _globalJsonPath));
            }
        }
        else if (ReferenceEquals(field, _accessModeField))
        {
            lines.Add(new DetailLine("Installs to:", _installPath));
        }
        else if (_migrateField is not null && ReferenceEquals(field, _migrateField) && choiceIndex == YesIndex)
        {
            lines.AddRange(BuildMigrationLines());
        }

        return new FieldDetail(helper, lines);
    }

    private List<DetailLine> BuildMigrationLines()
    {
        return _migrations
            .GroupBy(m => m.Component)
            .OrderBy(g => g.Key)
            .Select(g => new DetailLine(
                FormattableString.Invariant($"{g.Key.GetDisplayName()}s:"),
                FormatVersions([.. g.Select(m => m.ExampleVersion.ToString())])))
            .ToList();
    }

    // Joins the first few versions and collapses the rest into "and N more".
    private static string FormatVersions(IReadOnlyList<string> versions)
    {
        int shown = Math.Min(MaxVersionsShown, versions.Count);
        string joined = string.Join(", ", versions.Take(shown));

        int remaining = versions.Count - shown;
        return remaining > 0
            ? string.Format(CultureInfo.InvariantCulture, "{0}, and {1} more", joined, remaining)
            : joined;
    }

    /// <summary>
    /// Builds the form from the resolved <paramref name="plan"/>: an SDK channel field, a single
    /// access-mode field (its choices vary by platform), and — only when there are candidates — a
    /// migrate field.
    /// </summary>
    public static InitFormModel Create(WalkthroughPlan plan)
    {
        (FormField channelField, IReadOnlyList<string?> channelTokens, int globalJsonChannelIndex) =
            BuildChannelField(plan.ChannelDisplay);

        (FormField accessModeField, IReadOnlyList<DotnetAccessMode> accessModes) =
            BuildAccessModeField(plan.AccessMode);

        FormField? migrateField = plan.Migrations.Count > 0 ? BuildMigrateField() : null;

        var fields = new List<FormField> { channelField, accessModeField };
        if (migrateField is not null)
        {
            fields.Add(migrateField);
        }

        return new InitFormModel(
            fields,
            channelField,
            channelTokens,
            globalJsonChannelIndex,
            plan.ChannelDisplay.GlobalJsonPath,
            accessModeField,
            accessModes,
            migrateField,
            plan.Migrations,
            plan.InstallRoot.Path);
    }

    private static (FormField Field, IReadOnlyList<string?> Tokens, int GlobalJsonIndex) BuildChannelField(
        DefaultChannelDisplay channelDisplay)
    {
        var choices = new List<FieldChoice>();
        var tokens = new List<string?>();
        int globalJsonIndex = -1;

        // A channel implied by a nearby global.json is the recommended default, listed first.
        bool globalJsonImplied = channelDisplay.GlobalJsonPath is not null && channelDisplay.ChannelLabel is not null;
        if (globalJsonImplied)
        {
            globalJsonIndex = 0;
            choices.Add(new FieldChoice(channelDisplay.ChannelLabel!, "From your global.json"));
            tokens.Add(channelDisplay.ChannelLabel);
        }

        choices.Add(new FieldChoice(ChannelVersionResolver.LatestChannel, "Latest stable release"));
        tokens.Add(ChannelVersionResolver.LatestChannel);
        choices.Add(new FieldChoice(ChannelVersionResolver.LtsChannel, "Long Term Support"));
        tokens.Add(ChannelVersionResolver.LtsChannel);
        choices.Add(new FieldChoice(ChannelVersionResolver.PreviewChannel, "Latest preview"));
        tokens.Add(ChannelVersionResolver.PreviewChannel);
        choices.Add(new FieldChoice(ChannelVersionResolver.DailyChannel, "Latest unsigned daily build"));
        tokens.Add(ChannelVersionResolver.DailyChannel);
        choices.Add(new FieldChoice(InitWorkflows.NoneChannel, "Pick what to install later"));
        tokens.Add(InitWorkflows.NoneChannel);
        choices.Add(new FieldChoice("<other>", "Type your own, e.g. 8.0.4xx", IsCustomInput: true));
        tokens.Add(null);

        // The recommended default is listed first (the global.json channel when present, else "latest").
        int defaultIndex = 0;

        var field = new FormField("SDK Channel", choices, defaultIndex, inlineHelp: true);
        return (field, tokens, globalJsonIndex);
    }

    private static (FormField Field, IReadOnlyList<DotnetAccessMode> Modes) BuildAccessModeField(DotnetAccessMode recommended)
    {
        bool isWindows = OperatingSystem.IsWindows();

        var modes = new List<DotnetAccessMode> { DotnetAccessMode.None, DotnetAccessMode.Shell };
        var choices = new List<FieldChoice>
        {
            new(DotnetAccessModeDisplay.GetName(DotnetAccessMode.None), Strings.PathDescriptionNone),
            new(DotnetAccessModeDisplay.GetName(DotnetAccessMode.Shell), isWindows ? Strings.PathDescriptionShell : Strings.PathDescriptionShellBase),
        };

        if (isWindows)
        {
            modes.Add(DotnetAccessMode.Everywhere);
            choices.Add(new FieldChoice(DotnetAccessModeDisplay.GetName(DotnetAccessMode.Everywhere), Strings.PathDescriptionEverywhere));
        }

        int defaultIndex = Math.Max(0, modes.IndexOf(recommended));
        var field = new FormField("Access mode", choices, defaultIndex);
        return (field, modes);
    }

    private static FormField BuildMigrateField()
    {
        var choices = new List<FieldChoice>
        {
            new("Yes", "Bring your existing system-wide SDKs and runtimes under dotnetup's management so they are updated and cleaned up together."),
            new("No", "Leave existing system-wide .NET installs untouched. dotnetup manages only what it installs."),
        };

        return new FormField("Migrate system installs", choices, defaultIndex: YesIndex);
    }
}
