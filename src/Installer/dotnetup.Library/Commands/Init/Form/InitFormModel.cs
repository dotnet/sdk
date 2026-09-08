// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init.Form;

/// <summary>
/// Builds the init form's fields from resolved <see cref="InitFormDefaults"/> and computes the
/// per-field detail shown beside them, and translates the user's accepted selections back into the
/// domain values (channel, access mode, whether to migrate). This is the single place that knows
/// the field/value ↔ domain mapping; the renderer and state machine stay domain-agnostic.
///
/// Construction is side-effect-free and network-free: choices come from the defaults and static
/// channel tokens (concrete SDK versions are resolved later, only when an install actually runs),
/// so simply showing the form never triggers a download.
/// </summary>
internal sealed class InitFormModel
{
    // Maximum number of versions listed per migration component line before collapsing to "and N more".
    private const int MaxVersionsShown = 3;

    // Index of the "Yes" choice in the access-affecting yes/no fields (Migrate).
    private const int YesIndex = 0;

    // Index of the "No" choice in the access-affecting yes/no fields (Migrate).
    private const int NoIndex = 1;

    private readonly FormField _channelField;
    private readonly IReadOnlyList<string?> _channelTokens;
    private readonly int _globalJsonChannelIndex;
    private readonly string? _globalJsonPath;

    private readonly FormField _accessModeField;
    private readonly IReadOnlyList<DotnetAccessMode> _accessModes;

    private readonly FormField? _migrateField;
    private readonly IReadOnlyList<MigrationWorkflow.MigrationSelection> _migrationCandidates;
    private readonly IReadOnlyList<MinimalInstallSpec> _defaultInstallSpecs;

    private readonly IReadOnlyList<string> _profilePaths;

    private InitFormModel(
        IReadOnlyList<FormField> fields,
        FormField channelField,
        IReadOnlyList<string?> channelTokens,
        int globalJsonChannelIndex,
        string? globalJsonPath,
        FormField accessModeField,
        IReadOnlyList<DotnetAccessMode> accessModes,
        FormField? migrateField,
        IReadOnlyList<MigrationWorkflow.MigrationSelection> migrationCandidates,
        IReadOnlyList<MinimalInstallSpec> defaultInstallSpecs,
        string installPath,
        IReadOnlyList<string> profilePaths)
    {
        Fields = fields;
        _channelField = channelField;
        _channelTokens = channelTokens;
        _globalJsonChannelIndex = globalJsonChannelIndex;
        _globalJsonPath = globalJsonPath;
        _accessModeField = accessModeField;
        _accessModes = accessModes;
        _migrateField = migrateField;
        _migrationCandidates = migrationCandidates;
        _defaultInstallSpecs = defaultInstallSpecs;
        InstallPath = installPath;
        _profilePaths = profilePaths;
    }

    /// <summary>The form fields, in display order.</summary>
    public IReadOnlyList<FormField> Fields { get; }

    /// <summary>
    /// The channel the user chose: a fixed channel token or the typed custom value.
    /// </summary>
    public string? SelectedChannel()
    {
        if (_channelField.Selected.IsCustomInput)
        {
            return _channelField.CustomValue;
        }

        return _channelTokens[_channelField.SelectedIndex];
    }

    /// <summary>The access mode the user chose.</summary>
    public DotnetAccessMode SelectedAccessMode() => _accessModes[_accessModeField.SelectedIndex];

    /// <summary>Whether the user chose to migrate existing system installs.</summary>
    public bool MigrateSelected() =>
        _migrateField is { IsVisible: true } && _migrateField.SelectedIndex == YesIndex;

    /// <summary>Where dotnetup installs .NET; shown once at the top of the form.</summary>
    public string InstallPath { get; }

    /// <summary>
    /// Computes the derived informational lines for the given field's value at
    /// <paramref name="choiceIndex"/>. Static help text belongs to the corresponding
    /// <see cref="FieldChoice"/>.
    /// </summary>
    public IReadOnlyList<DetailLine> BuildDerivedDetailLines(FormField field, int choiceIndex)
    {
        var lines = new List<DetailLine>();

        if (ReferenceEquals(field, _channelField))
        {
            if (choiceIndex == _globalJsonChannelIndex && _globalJsonPath is not null)
            {
                lines.Add(new DetailLine(Strings.InitFormDetailGlobalJsonSource, _globalJsonPath));
            }
        }
        else if (ReferenceEquals(field, _accessModeField))
        {
            lines.AddRange(BuildAccessModeLines(_accessModes[choiceIndex]));
        }
        else if (_migrateField is not null && ReferenceEquals(field, _migrateField) && choiceIndex == YesIndex)
        {
            lines.AddRange(BuildMigrationLines());
        }

        return lines;
    }

    // The concrete artifacts an access mode produces (the shell profile it edits, the env vars and
    // system-PATH changes it makes). Prose is left to the option's help text; these are just facts,
    // shown beneath the highlighted option when the field is expanded.
    private List<DetailLine> BuildAccessModeLines(DotnetAccessMode mode)
    {
        switch (mode)
        {
            case DotnetAccessMode.None:
                return [];

            case DotnetAccessMode.Shell:
            {
                var lines = new List<DetailLine>
                {
                    new(Strings.InitFormDetailShellOnly),
                };
                AddProfileLines(lines);
                return lines;
            }

            case DotnetAccessMode.Everywhere:
            {
                var lines = new List<DetailLine>();
                AddProfileLines(lines);
                lines.Add(new DetailLine(Strings.InitFormDetailSystemPath));
                lines.Add(new DetailLine(Strings.InitFormDetailDotnetRoot));
                return lines;
            }

            default:
                return [];
        }
    }

    private void AddProfileLines(List<DetailLine> lines)
    {
        if (_profilePaths.Count == 0)
        {
            lines.Add(new DetailLine(Strings.InitFormDetailEditShellProfile));
            return;
        }

        foreach (string profilePath in _profilePaths)
        {
            lines.Add(new DetailLine(Strings.InitFormDetailEdits, profilePath));
        }
    }

    private List<DetailLine> BuildMigrationLines()
    {
        return CurrentMigrations()
            .GroupBy(m => m.Component)
            .OrderBy(g => g.Key)
            .Select(g => new DetailLine(
                GetMigrationComponentLabel(g.Key),
                FormatVersions([.. g.Select(m => m.ExampleVersion.ToString())])))
            .ToList();
    }

    private static string GetMigrationComponentLabel(InstallComponent component) => component switch
    {
        InstallComponent.SDK => Strings.InitFormMigrationSdksLabel,
        InstallComponent.Runtime => Strings.InitFormMigrationRuntimesLabel,
        InstallComponent.ASPNETCore => Strings.InitFormMigrationAspNetCoreRuntimesLabel,
        InstallComponent.WindowsDesktop => Strings.InitFormMigrationWindowsDesktopRuntimesLabel,
        _ => component.ToString(),
    };

    private List<MigrationWorkflow.MigrationSelection> CurrentMigrations()
    {
        return MigrationWorkflow.FilterMigrationSelections(
            _migrationCandidates,
            GetCurrentInstallSpecs(_channelField, _channelTokens, _defaultInstallSpecs));
    }

    // Joins the first few versions and collapses the rest into "and N more".
    private static string FormatVersions(IReadOnlyList<string> versions)
    {
        int shown = Math.Min(MaxVersionsShown, versions.Count);
        string joined = string.Join(", ", versions.Take(shown));

        int remaining = versions.Count - shown;
        return remaining > 0
            ? string.Format(CultureInfo.InvariantCulture, Strings.InitFormMigrationAdditionalVersions, joined, remaining)
            : joined;
    }

    /// <summary>
    /// Builds the form from the resolved <paramref name="defaults"/>: an SDK channel field, a single
    /// access-mode field (its choices vary by platform), and — only when there are candidates — a
    /// migrate field. The <paramref name="shellProvider"/> supplies the profile file(s) shown in the
    /// access-mode detail; it may be null when no supported shell is detected.
    /// </summary>
    public static InitFormModel Create(InitFormDefaults defaults, IEnvShellProvider? shellProvider)
    {
        (FormField channelField, IReadOnlyList<string?> channelTokens, int globalJsonChannelIndex) =
            BuildChannelField(defaults.ChannelDisplay);

        (FormField accessModeField, IReadOnlyList<DotnetAccessMode> accessModes) =
            BuildAccessModeField(defaults.AccessMode);

        FormField? migrateField = null;
        if (defaults.Migrations.Count > 0)
        {
            migrateField = BuildMigrateField(
                defaults.MigrateSystemInstalls,
                isVisible: () =>
                {
                    return MigrationWorkflow.FilterMigrationSelections(
                        defaults.Migrations,
                        GetCurrentInstallSpecs(channelField, channelTokens, defaults.DefaultInstallSpecs)).Count > 0;
                });
        }

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
            defaults.ChannelDisplay.GlobalJsonPath,
            accessModeField,
            accessModes,
            migrateField,
            defaults.Migrations,
            defaults.DefaultInstallSpecs,
            defaults.InstallRoot.Path,
            shellProvider?.GetProfilePaths() ?? []);
    }

    private static (FormField Field, IReadOnlyList<string?> Tokens, int GlobalJsonIndex) BuildChannelField(
        DefaultChannelDisplay channelDisplay)
    {
        var choices = new List<FieldChoice>();
        var tokens = new List<string?>();
        int globalJsonIndex = -1;

        // A channel supplied by the pending install request is the recommended default, listed
        // first. It may have come from global.json or directly from the command line.
        if (channelDisplay.ChannelLabel is not null)
        {
            if (channelDisplay.GlobalJsonPath is not null)
            {
                globalJsonIndex = 0;
            }

            string helperText = channelDisplay.GlobalJsonPath is not null
                ? Strings.InitFormChannelFromGlobalJson
                : Strings.InitFormChannelRequested;
            choices.Add(new FieldChoice(channelDisplay.ChannelLabel, helperText));
            tokens.Add(channelDisplay.ChannelLabel);
        }

        AddChannelChoice(ChannelVersionResolver.LatestChannel, Strings.InitFormChannelLatestHelp);
        AddChannelChoice(ChannelVersionResolver.LtsChannel, Strings.InitFormChannelLtsHelp);
        AddChannelChoice(ChannelVersionResolver.PreviewChannel, Strings.InitFormChannelPreviewHelp);
        AddChannelChoice(ChannelVersionResolver.DailyChannel, Strings.InitFormChannelDailyHelp);
        choices.Add(new FieldChoice(
            Strings.InitFormChannelOther,
            Strings.InitFormChannelOtherHelp,
            IsCustomInput: true));
        tokens.Add(null);

        // The recommended default is listed first (the pending request when present, else "latest").
        int defaultIndex = 0;

        var field = new FormField(
            Strings.InitFormChannelLabel,
            choices,
            defaultIndex,
            description: Strings.InitFormChannelDescription,
            inlineHelp: true,
            browseDetailShowsDescription: true);
        return (field, tokens, globalJsonIndex);

        void AddChannelChoice(string channel, string helperText)
        {
            if (string.Equals(channelDisplay.ChannelLabel, channel, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            choices.Add(new FieldChoice(channel, helperText));
            tokens.Add(channel);
        }
    }

    private static (FormField Field, IReadOnlyList<DotnetAccessMode> Modes) BuildAccessModeField(DotnetAccessMode recommended)
    {
        bool isWindows = OperatingSystem.IsWindows();

        var modes = new List<DotnetAccessMode> { DotnetAccessMode.None, DotnetAccessMode.Shell };
        var choices = new List<FieldChoice>
        {
            new(DotnetAccessMode.None.ToString().ToLowerInvariant(), Strings.InitFormAccessModeNoneHelp),
            new(DotnetAccessMode.Shell.ToString().ToLowerInvariant(), Strings.InitFormAccessModeShellHelp),
        };

        if (isWindows)
        {
            modes.Add(DotnetAccessMode.Everywhere);
            choices.Add(new FieldChoice(
                DotnetAccessMode.Everywhere.ToString().ToLowerInvariant(),
                Strings.InitFormAccessModeEverywhereHelp));
        }

        int defaultIndex = Math.Max(0, modes.IndexOf(recommended));
        var field = new FormField(
            Strings.InitFormAccessModeLabel,
            choices,
            defaultIndex,
            description: Strings.InitFormAccessModeDescription);
        return (field, modes);
    }

    private static FormField BuildMigrateField(bool migrateByDefault, Func<bool> isVisible)
    {
        var choices = new List<FieldChoice>
        {
            new(Strings.InitFormChoiceYes, Strings.InitFormMigrateYesHelp),
            new(Strings.InitFormChoiceNo, Strings.InitFormMigrateNoHelp),
        };

        return new FormField(
            Strings.InitFormMigrateLabel,
            choices,
            defaultIndex: migrateByDefault ? YesIndex : NoIndex,
            isVisible: isVisible);
    }

    private static string? GetSelectedChannel(
        FormField channelField,
        IReadOnlyList<string?> channelTokens)
    {
        if (channelField.Selected.IsCustomInput)
        {
            return channelField.CustomValue;
        }

        return channelTokens[channelField.SelectedIndex];
    }

    private static IReadOnlyCollection<MinimalInstallSpec> GetCurrentInstallSpecs(
        FormField channelField,
        IReadOnlyList<string?> channelTokens,
        IReadOnlyList<MinimalInstallSpec> defaultInstallSpecs)
    {
        string? selectedChannel = GetSelectedChannel(channelField, channelTokens);
        if (selectedChannel is null)
        {
            return [];
        }

        return InitWorkflows.SelectedChannelDiffersFromDefault(
            selectedChannel,
            channelTokens[channelField.DefaultIndex])
            ? [.. defaultInstallSpecs.Select(spec => new MinimalInstallSpec(spec.Component, selectedChannel))]
            : defaultInstallSpecs;
    }
}
