// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Runtime.ExceptionServices;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Init.Form;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;
using Spectre.Console;
using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;

/// <summary>
/// Orchestrates the interactive init/onboarding flow that configures the user's
/// environment and records the access mode to
/// <c>dotnetup.config.json</c>.
/// </summary>
internal class InitWorkflows
{
    private readonly IDotnetEnvironmentManager _dotnetEnvironment;

    public InitWorkflows(IDotnetEnvironmentManager dotnetEnvironment)
    {
        _dotnetEnvironment = dotnetEnvironment;
    }

    // ── Init Flow Orchestrators ──

    /// <summary>
    /// Interactive onboarding flow used both by the explicit <c>dotnetup init</c> command
    /// and by the first interactive install when dotnetup has not yet been configured.
    /// Resolves the recommended setup, and shows the form where the options can be reviewed and modified.
    /// When <paramref name="requests"/> is supplied, those already-resolved install requests are
    /// reused as the recommended requests instead of resolving the default SDK channel.
    /// </summary>
    public List<ResolvedInstallRequest> InitWalkthrough(
        InstallCommand command,
        List<ResolvedInstallRequest>? requests = null)
    {
        // When a nearby global.json pins a local SDK via "sdk.paths", dotnetup is not the
        // environment owner for this directory: skip onboarding (the form, access-mode setup, and
        // migration) so we don't point the environment at a repo-local path or migrate system
        // installs. 'dotnetup install' can still install .NET to that path. Dry-run ignores
        // global.json entirely so the form can be previewed from any directory.
        if (!command.DryRun && HasLocalSdkPathGlobalJson())
        {
            SpectreAnsiConsole.MarkupLine(
                $"[{DotnetupTheme.Current.Dim}]A global.json here specifies a local .NET SDK path, so dotnetup left your environment unchanged. Use 'dotnetup install' to install .NET to that path.[/]");
            return [];
        }

        ShowBanner();

        DotnetupConfigData? previousConfig = DotnetupConfig.Read();

        // Resolve the recommended setup. This is side-effect-free: it performs no version
        // resolution, writes no output, and does not throw on an unresolvable channel, so simply
        // viewing the form or choosing to exit never triggers an install or a download. Dry-run
        // ignores global.json so the preview reflects a normal directory.
        InitFormDefaults defaults = InitDefaultsResolver.ResolveFormDefaults(
            command,
            requests,
            _dotnetEnvironment,
            configuredAccessMode: previousConfig?.AccessMode,
            ignoreGlobalJson: command.DryRun);

        // Show the interactive form (or, non-interactively, take the recommended defaults) and read
        // back the user's raw choices without resolving any install requests yet.
        FormOutcome? outcome = ResolveFormOutcome(command, defaults);
        if (outcome is null)
        {
            return []; // User chose to exit without changes.
        }

        if (command.DryRun)
        {
            PrintDryRunPreview(defaults, outcome);
            return [];
        }

        WalkthroughSelection selection = BuildSelection(command, requests, defaults, outcome);
        return ExecuteWalkthroughSelection(command, selection, defaults.InstallRoot, previousConfig);
    }

    /// <summary>
    /// Runs the interactive init form (when interactive) and reads the user's choices into a
    /// <see cref="FormOutcome"/>, or returns null when the user exits. In non-interactive sessions
    /// the same defaults are used without prompting.
    /// </summary>
    private static FormOutcome? ResolveFormOutcome(InstallCommand command, InitFormDefaults defaults)
    {
        bool interactive = command.Interactive && !Console.IsInputRedirected;
        if (!interactive)
        {
            return new FormOutcome(
                Channel: null,
                AccessMode: defaults.AccessMode,
                Migrate: defaults.MigrateSystemInstalls);
        }

        InitFormModel model = InitFormModel.Create(
            defaults,
            command.ShellProvider ?? ShellDetection.GetCurrentShellProvider());
        if (!InteractiveFormSelector.Show(model))
        {
            return null;
        }

        return new FormOutcome(
            Channel: model.SelectedChannel(),
            AccessMode: model.SelectedAccessMode(),
            Migrate: model.MigrateSelected());
    }

    /// <summary>
    /// Turns the user's <see cref="FormOutcome"/> into a <see cref="WalkthroughSelection"/>,
    /// resolving concrete install requests only now (this may resolve versions / hit the network),
    /// which is why dry-run stops before this step.
    /// </summary>
    private static WalkthroughSelection BuildSelection(
        InstallCommand command,
        List<ResolvedInstallRequest>? requests,
        InitFormDefaults defaults,
        FormOutcome outcome)
    {
        List<ResolvedInstallRequest> effectiveRequests;
        if (!SelectedChannelDiffersFromDefault(outcome.Channel, defaults.ChannelDisplay.ChannelLabel))
        {
            effectiveRequests = InitDefaultsResolver.ResolveDefaultRequests(command, requests);
        }
        else
        {
            effectiveRequests = InitDefaultsResolver.GenerateInstallRequests(
                command,
                BuildChangedChannelSpecs(requests, outcome.Channel));
        }

        List<MigrationWorkflow.MigrationSelection> migrations = outcome.Migrate
            ? MigrationWorkflow.FilterMigrationSelections(defaults.Migrations, effectiveRequests)
            : [];
        return new WalkthroughSelection(effectiveRequests, outcome.AccessMode, migrations);
    }

    internal static MinimalInstallSpec[] BuildChangedChannelSpecs(
        List<ResolvedInstallRequest>? requests,
        string? channel)
    {
        return requests is { Count: > 0 }
            ? [.. requests.Select(request => new MinimalInstallSpec(request.Request.Component, channel))]
            : [new MinimalInstallSpec(InstallComponent.SDK, channel)];
    }

    /// <summary>
    /// Prints what the accepted settings would do, without installing or changing the environment.
    /// Kept network-free: it echoes the chosen channel rather than resolving a concrete version.
    /// </summary>
    private static void PrintDryRunPreview(InitFormDefaults defaults, FormOutcome outcome)
    {
        string dim = DotnetupTheme.Current.Dim;
        string accent = DotnetupTheme.Current.Accent;

        SpectreAnsiConsole.MarkupLine($"[{dim}](dry run \u2014 no changes were made to your machine)[/]");

        string channelText =
            outcome.Channel ?? defaults.ChannelDisplay.ChannelLabel ?? ChannelVersionResolver.LatestChannel;
        List<MigrationWorkflow.MigrationSelection> migrations = MigrationWorkflow.FilterMigrationSelections(
            defaults.Migrations,
            BuildSelectedInstallSpecs(defaults, outcome));
        string migrateText = outcome.Migrate
            ? string.Format(CultureInfo.InvariantCulture, "Yes ({0} install(s))", migrations.Count)
            : "No";

        PrintPreviewLine("SDK channel", channelText, accent);
        PrintPreviewLine("Access mode", outcome.AccessMode.ToString(), accent);
        PrintPreviewLine("Migrate system installs", migrateText, accent);
        PrintPreviewLine("Installs in", defaults.InstallRoot.Path, accent);
    }

    private static void PrintPreviewLine(string label, string value, string accent)
    {
        SpectreAnsiConsole.MarkupLine(
            $"  [white]{label.EscapeMarkup()}:[/]  [{accent}]{value.EscapeMarkup()}[/]");
    }

    private static MinimalInstallSpec[] BuildSelectedInstallSpecs(InitFormDefaults defaults, FormOutcome outcome)
    {
        return SelectedChannelDiffersFromDefault(outcome.Channel, defaults.ChannelDisplay.ChannelLabel)
            ? [.. defaults.DefaultInstallSpecs.Select(spec => new MinimalInstallSpec(spec.Component, outcome.Channel))]
            : [.. defaults.DefaultInstallSpecs];
    }

    internal static bool SelectedChannelDiffersFromDefault(string? selectedChannel, string? defaultChannel) =>
        selectedChannel is not null
        && !string.Equals(
            selectedChannel,
            defaultChannel,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Installs the selected requests (with any migrations), persists the configuration, and
    /// applies the environment changes for the chosen mode.
    /// </summary>
    private List<ResolvedInstallRequest> ExecuteWalkthroughSelection(
        InstallCommand command,
        WalkthroughSelection selection,
        DotnetInstallRoot defaultInstallRoot,
        DotnetupConfigData? previousConfig)
    {
        List<ResolvedInstallRequest> effectiveRequests = selection.Requests;
        DotnetAccessMode accessMode = selection.AccessMode;

        // Start the predownload now that the install requests are known, so the cache populates
        // while the config is written.
        Task? predownloadTask = effectiveRequests.Count > 0
            ? InstallerOrchestratorSingleton.PredownloadToCacheAsync(effectiveRequests[0])
            : null;

        DotnetInstallRoot installRoot = GetInstallRootOrDefault(effectiveRequests, defaultInstallRoot);
        string? manifestPath = GetManifestPath(effectiveRequests);

        // A failed install (e.g. one unavailable runtime version) must not prevent us from
        // configuring the environment for the installs that DID succeed. The install step is
        // already best-effort — it installs every available request and then throws for the
        // failures — so we capture that failure, finish applying configuration below, and then
        // rethrow (before printing "Setup complete!") so the error still surfaces to the caller
        // (and telemetry).
        ExceptionDispatchInfo? installFailure = null;
        try
        {
            if (selection.Migrations.Count > 0)
            {
                effectiveRequests = RunInstallsWithMigration(
                    command, effectiveRequests, selection.Migrations, installRoot, manifestPath, predownloadTask);
            }
            else
            {
                RunInstallRequests(effectiveRequests, predownloadTask, command.NoProgress, command);
            }
        }
        catch (DotnetInstallException ex)
        {
            installFailure = ExceptionDispatchInfo.Capture(ex);
        }

        DisplayEnvironmentSetupProgress(SpectreAnsiConsole.Console);

        // Save config and apply configuration(s) regardless of partial install failure, so the
        // user's choice persists and the successful installs are usable (PATH / shell profile).
        bool dotnetupOnPath = InitDefaultsResolver.GetDefaultDotnetupOnPath(previousConfig);
        SaveConfig(accessMode, dotnetupOnPath);

        ObservedEnvironmentState observed = new EnvironmentStateInspector(_dotnetEnvironment)
            .Inspect(command.ShellProvider ?? ShellDetection.GetCurrentShellProvider());
        EnvSettingsApplier.Apply(
            accessMode,
            dotnetupOnPath,
            observed,
            _dotnetEnvironment,
            installRoot.Path,
            command.ShellProvider);

        // One or more installs failed; surface the error after configuration was applied.
        installFailure?.Throw();

        DisplaySetupResult(accessMode, previousConfig?.AccessMode);

        return effectiveRequests;
    }

    /// <summary>
    /// Returns the first request's install root when any requests exist, otherwise the fallback.
    /// </summary>
    private static DotnetInstallRoot GetInstallRootOrDefault(
        List<ResolvedInstallRequest> requests,
        DotnetInstallRoot fallback)
        => requests.Count > 0 ? requests[0].Request.InstallRoot : fallback;

    /// <summary>
    /// Returns the manifest path carried by the first request, or null when there are no requests.
    /// </summary>
    private static string? GetManifestPath(List<ResolvedInstallRequest> requests)
        => requests.Count > 0 ? requests[0].Request.Options.ManifestPath : null;

    /// <summary>
    /// Two-phase install used by the init walkthrough when migrations were selected.
    /// Phase 1: existing requests + SDK migrations. Phase 2: runtime-style migrations
    /// not already on disk after Phase 1 (avoids re-downloading runtimes the SDK brought down).
    /// </summary>
    private static List<ResolvedInstallRequest> RunInstallsWithMigration(
        InstallCommand command,
        List<ResolvedInstallRequest> effectiveRequests,
        List<MigrationWorkflow.MigrationSelection> toMigrate,
        DotnetInstallRoot installRoot,
        string? manifestPath,
        Task? predownloadTask)
    {
        return MigrationWorkflow.ExecuteMigrationInPhases(
            effectiveRequests, toMigrate, command, installRoot, manifestPath,
            runner: requests =>
            {
                if (requests.Count > 0)
                {
                    DisplayInstallLocation(requests[0]);
                }
                // Wait for the predownload to finish (if still running) before starting the real install,
                // so the cache is populated and we avoid redundant downloads. Only meaningful for Phase 1.
                predownloadTask?.GetAwaiter().GetResult();
                predownloadTask = null;
                InstallExecutor.ExecuteInstallsAndThrowOnFailure(requests, command.NoProgress, command);
            });
    }

    private static void RunInstallRequests(
        List<ResolvedInstallRequest> requests,
        Task? predownloadTask,
        bool noProgress,
        CommandBase command)
    {
        if (requests.Count > 0)
        {
            DisplayInstallLocation(requests[0]);
        }

        // Wait for the predownload to finish (if still running) before starting the real install,
        // so the cache is populated and we avoid redundant downloads.
        predownloadTask?.GetAwaiter().GetResult();

        InstallExecutor.ExecuteInstallsAndThrowOnFailure(requests, noProgress, command);
    }

    internal static void DisplayEnvironmentSetupProgress(IAnsiConsole console)
        => console.MarkupLine("Setting up your environment.");

    // ── Prompt Functions ──

    /// <summary>
    /// Prompts the user about migrating system installs into the dotnetup-managed directory.
    /// Existing installs are normalized to update channels and deduplicated before prompting.
    /// </summary>
    /// <returns>A list of deduplicated channel selections to migrate, or an empty list if the user declines or no candidates remain.</returns>
    internal static List<MigrationWorkflow.MigrationSelection> PromptInstallsToMigrateIfDesired(
        IDotnetEnvironmentManager dotnetEnvironment,
        DotnetInstallRoot installRoot,
        string? manifestPath = null,
        IReadOnlyCollection<ResolvedInstallRequest>? existingRequests = null,
        bool interactive = true)
    {
        if (!interactive)
        {
            return [];
        }

        var migrationSelections = InitDefaultsResolver.ResolveDefaultMigrations(
            dotnetEnvironment, installRoot, manifestPath);
        if (existingRequests is not null)
        {
            migrationSelections = MigrationWorkflow.FilterMigrationSelections(migrationSelections, existingRequests);
        }

        if (migrationSelections.Count == 0)
        {
            return [];
        }

        return PromptUserForMigration(migrationSelections, dotnetEnvironment);
    }

    internal static List<string> FormatMigrationDisplayItems(List<MigrationWorkflow.MigrationSelection> migrationSelections)
    {
        bool showArchitecture = migrationSelections
            .Select(i => i.Architecture)
            .Distinct()
            .Skip(1)
            .Any();

        return migrationSelections
            .OrderBy(i => i.Component)
            .ThenBy(i => i.Channel.Name)
            .Select(i => showArchitecture
                ? string.Format(CultureInfo.InvariantCulture, "{0} {1} [{2}]", i.Component.GetDisplayName(), i.Channel.Name, i.Architecture)
                : string.Format(CultureInfo.InvariantCulture, "{0} {1}", i.Component.GetDisplayName(), i.Channel.Name))
            .ToList();
    }

    internal static List<MigrationWorkflow.MigrationSelection> PromptUserForMigration(
        List<MigrationWorkflow.MigrationSelection> migrationSelections,
        IDotnetEnvironmentManager dotnetEnvironment)
    {
        if (Console.IsInputRedirected)
        {
            SpectreAnsiConsole.MarkupLine(
                $"[{DotnetupTheme.Current.Dim}]Skipping the migration prompt because interactive input is not available. {GetMigrationRetryHint().EscapeMarkup()}[/]");
            return [];
        }

        // Find the system install path for display purposes. Whether the dotnet winning on PATH is
        // a dotnetup hive is irrelevant here; we want its location only when it is a system install.
        var currentInstall = dotnetEnvironment.GetCurrentPathConfiguration();
        string systemPath = currentInstall is not null && InstallPathClassifier.IsAdminInstallPath(currentInstall.Path)
            ? currentInstall.Path
            : DotnetEnvironmentManager.GetSystemDotnetPaths().FirstOrDefault() ?? "the system .NET location";

        SpectreAnsiConsole.MarkupLine($"You have existing system-managed .NET installs in [{DotnetupTheme.Current.Accent}]{systemPath.EscapeMarkup()}[/].");

        var displayItems = FormatMigrationDisplayItems(migrationSelections);

        var confirmResult = SpectreDisplayHelpers.RenderScrollableListWithConfirm(
            displayItems,
            visibleCount: MigrationWorkflow.MigrationPreviewCount,
            "Do you want dotnetup to install matching versions in its managed directory?");

        HandleMigrationConfirmResult(confirmResult);
        return confirmResult == ConfirmResult.Yes ? migrationSelections : [];
    }

    /// <summary>
    /// Writes the follow-up message after the user accepts or declines the migration prompt.
    /// </summary>
    private static void HandleMigrationConfirmResult(ConfirmResult confirmResult)
    {
        if (confirmResult == ConfirmResult.Yes)
        {
            SpectreAnsiConsole.MarkupLine($"[{DotnetupTheme.Current.Dim}]These will be installed as part of the current setup.[/]");
        }
        else
        {
            SpectreAnsiConsole.MarkupLine($"[{DotnetupTheme.Current.Dim}]{GetMigrationRetryHint().EscapeMarkup()}[/]");
        }
    }

    private static string GetMigrationRetryHint()
        => "You can migrate matching SDKs or runtimes later with \"dotnetup sdk install --migrate-from-system\" or \"dotnetup runtime install --migrate-from-system\".";

    // ── Display Functions ──

    /// <summary>
    /// Shows the user where .NET will be installed, noting if the path
    /// was determined by a global.json file.
    /// </summary>
    private static void DisplayInstallLocation(ResolvedInstallRequest request)
    {
        string? globalJsonPath = request.Request.Options.GlobalJsonPath;
        string installPath = request.Request.InstallRoot.Path;

        if (globalJsonPath is not null)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(
                CultureInfo.InvariantCulture,
                "[{0}]Installing to [{1}]{2}[/] as specified by [{1}]{3}[/].[/]",
                DotnetupTheme.Current.Dim,
                DotnetupTheme.Current.Accent,
                installPath.EscapeMarkup(),
                globalJsonPath.EscapeMarkup()));
        }
        else
        {
            SpectreAnsiConsole.MarkupLine(string.Format(
                CultureInfo.InvariantCulture,
                "[{0}]You can find dotnetup managed installs at [{1}]{2}[/].[/]",
                DotnetupTheme.Current.Dim,
                DotnetupTheme.Current.Accent,
                installPath.EscapeMarkup()));
        }
    }

    /// <summary>
    /// True when a nearby global.json pins a local SDK via "sdk.paths". In that case dotnetup is not
    /// the environment owner for the directory, so first-run onboarding is skipped.
    /// </summary>
    internal static bool HasLocalSdkPathGlobalJson()
        => GlobalJsonModifier.GetGlobalJsonInfo(Environment.CurrentDirectory).SdkPath is not null;

    private static void ShowBanner()
    {
        SpectreAnsiConsole.Write(DotnetBotBanner.BuildPanel());
        SpectreAnsiConsole.WriteLine();
    }

    /// <summary>
    /// Writes the dotnetup config capturing the user's access mode. Safe to call on the
    /// failure path so the choice persists even when an install did not complete.
    /// </summary>
    private static void SaveConfig(DotnetAccessMode accessMode, bool dotnetupOnPath)
        => DotnetupConfig.Write(new DotnetupConfigData { AccessMode = accessMode, DotnetupOnPath = dotnetupOnPath });

    private static void DisplaySetupResult(DotnetAccessMode accessMode, DotnetAccessMode? previousAccessMode)
    {
        // Only show guidance when the access mode actually changed (or first-time setup).
        if (previousAccessMode != accessMode)
        {
            DisplayPathGuidance(accessMode);
        }

        SpectreAnsiConsole.MarkupLine(DotnetupTheme.Brand("Setup complete!"));
    }

    /// <summary>
    /// Shows guidance based on the chosen access mode.
    /// </summary>
    private static void DisplayPathGuidance(DotnetAccessMode accessMode)
    {
        string? guidance = accessMode switch
        {
            DotnetAccessMode.None => Strings.PathGuidanceNone,
            DotnetAccessMode.Shell => Strings.PathGuidanceShell,
            DotnetAccessMode.Everywhere => Strings.PathGuidanceEverywhere,
            _ => null,
        };

        if (guidance is not null)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(
                CultureInfo.InvariantCulture,
                "[{0}]{1}[/]",
                DotnetupTheme.Current.Dim,
                guidance.EscapeMarkup()));
        }
    }
}
