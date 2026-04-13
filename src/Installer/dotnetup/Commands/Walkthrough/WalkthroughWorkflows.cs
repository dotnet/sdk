// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Spectre.Console;
using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Walkthrough;

/// <summary>
/// Orchestrates the interactive walkthrough that configures the user's environment
/// and records the path replacement preference to <c>dotnetup.config.json</c>.
/// Has two modes:
/// <list type="bullet">
/// <item><see cref="FullIntroductionWalkthrough"/> — full first-run experience with channel prompt</item>
/// <item><see cref="BaseConfigurationWalkthrough"/> — minimal setup, wraps an install action</item>
/// </list>
/// </summary>
internal class WalkthroughWorkflows
{
    private readonly IDotnetEnvironmentManager _dotnetEnvironment;
    private readonly ChannelVersionResolver _channelVersionResolver;

    /// <summary>Sentinel channel value indicating the user wants to skip the initial install.</summary>
    internal const string NoneChannel = "none";

    private sealed record ChannelExample(string Channel, string Description, string? ResolvedVersion);

    public WalkthroughWorkflows(IDotnetEnvironmentManager dotnetEnvironment, ChannelVersionResolver channelVersionResolver)
    {
        _dotnetEnvironment = dotnetEnvironment;
        _channelVersionResolver = channelVersionResolver;
    }

    /// <summary>
    /// Returns true when the given <see cref="PathPreference"/> implies we should
    /// replace the default dotnet installation (i.e. update PATH / DOTNET_ROOT).
    /// </summary>
    public static bool ShouldReplaceSystemConfiguration(PathPreference preference) =>
        preference == PathPreference.FullPathReplacement;

    /// <summary>
    /// Returns true when the user chose to convert existing system-level .NET installs
    /// into dotnetup-managed installs. This applies to any mode that shadows the system PATH.
    /// Also returns false if the user previously opted out via <see cref="DotnetupConfigData.DisableInstallConversion"/>.
    /// </summary>
    public static bool ShouldPromptToConvertSystemInstalls(PathPreference preference, bool ignoreConfig = false)
    {
        if (preference == PathPreference.DotnetupDotnet)
        {
            return false;
        }

        if (!ignoreConfig)
        {
            var existingConfig = DotnetupConfig.Read();
            if (existingConfig?.DisableInstallConversion == true)
            {
                return false;
            }
        }

        return true;
    }

    // ── Walkthrough Orchestrators ──

    /// <summary>
    /// Full first-run walkthrough: shows banner, prompts for channel, generates
    /// install request, then delegates to <see cref="BaseConfigurationWalkthrough"/>
    /// for environment setup and installation.
    /// </summary>
    public void FullIntroductionWalkthrough(InstallCommand command)
    {
        ShowBanner();

        // Step 0: Explain channels and let the user pick one
        string selectedChannel = PromptChannel();

        if (selectedChannel == NoneChannel)
        {
            // User chose to skip installation — just configure the environment.
            BaseConfigurationWalkthrough(
                [],
                () => { },
                command.NoProgress);
            return;
        }

        // Generate the install request via the workflow (handles path resolution, global.json, validation)
        var workflow = new InstallWorkflow(command);
        var requests = workflow.GenerateInstallRequests(
            [new MinimalInstallSpec(InstallComponent.SDK, selectedChannel)]);

        BaseConfigurationWalkthrough(
            requests,
            () => InstallExecutor.ExecuteInstalls(requests, command.NoProgress),
            command.NoProgress);
    }

    /// <summary>
    /// Minimal walkthrough: prompts for path preference and admin migration (if needed),
    /// then runs the provided action, saves config, applies system configuration, and
    /// batch-installs any migrated system installs.
    /// Called by <see cref="FullIntroductionWalkthrough"/> and by <see cref="InstallWorkflow"/>
    /// when no explicit install path is provided.
    /// </summary>
    /// <param name="requests">The resolved install requests (used for predownload and install root context).</param>
    /// <param name="primaryActionAfterConfigured">The action to execute after environment configuration (typically the install).</param>
    /// <param name="noProgress">Whether to suppress progress display.</param>
    /// <param name="interactive">Whether to prompt the user. When false, uses existing config or defaults — no prompts are shown.</param>
    /// <param name="deferAdminMigrationUntilEnd">When true, defers the admin migration prompt until the end of the walkthrough.</param>
    /// <param name="askEvenIfConfigured">When true, prompts the user even if a preference was previously saved.</param>
    public void BaseConfigurationWalkthrough(
        List<ResolvedInstallRequest> requests,
        Action primaryActionAfterConfigured,
        bool noProgress,
        bool interactive = true,
        bool deferAdminMigrationUntilEnd = false,
        bool askEvenIfConfigured = true)
    {
        // Determine the install root for environment configuration and migration.
        // Use the first request's root if available, otherwise fall back to the default path.
        DotnetInstallRoot installRoot = requests.Count > 0
            ? requests[0].Request.InstallRoot
            : new DotnetInstallRoot(
                _dotnetEnvironment.GetDefaultDotnetInstallPath(),
                InstallerUtilities.GetDefaultInstallArchitecture());

        // Fire off background predownload while the user answers prompts.
        Task? predownloadTask = requests.Count > 0
            ? InstallerOrchestratorSingleton.PredownloadToCacheAsync(requests[0])
            : null;

        // User chooses how to access .NET
        PathPreference? previousPreference = DotnetupConfig.ReadPathPreference();
        var pathPreference = GetPathPreference(interactive, askEvenIfConfigured);
        string? manifestPath = requests.Count > 0 ? requests[0].Request.Options.ManifestPath : null;

        // (Can Defer) Step 2: Prompt about admin installs before setting up the environment.
        // In non-interactive mode, skip the migration prompt entirely.
        List<DotnetInstall> toMigrate = deferAdminMigrationUntilEnd
            ? []
            : PromptInstallsToMigrateIfDesired(_dotnetEnvironment, pathPreference, installRoot, manifestPath, askEvenIfConfigured);

        // Step 3: Run the primary action (typically installing the base SDK from global.json/latest).
        RunPrimaryInstall(requests, primaryActionAfterConfigured, predownloadTask);

        // Save config and apply configuration(s) - NOTE: Terminal Profile not yet implemented.
        SaveConfigAndDisplayResult(pathPreference, previousPreference);

        if (ShouldReplaceSystemConfiguration(pathPreference))
        {
            _dotnetEnvironment.ApplyEnvironmentModifications(InstallType.User, installRoot.Path);
        }

        // Step 4: Prompt migrating admin installs now that the environment is configured (if deferred).
        // NOTE: Global.json modification is intentionally NOT done here.
        // The walkthrough does not own global.json updates — that responsibility
        // belongs to InstallWorkflow, gated on the --update-global-json flag
        // which only the SDK install command exposes.
        if (deferAdminMigrationUntilEnd)
        {
            toMigrate = PromptInstallsToMigrateIfDesired(
                _dotnetEnvironment, pathPreference, installRoot, manifestPath, askEvenIfConfigured);
        }

        if (toMigrate.Count > 0)
        {
            SpectreAnsiConsole.MarkupLine(DotnetupTheme.Dim(
                "You may now use dotnetup. In the meantime, we'll install your remaining components."));
            ExecuteMigrationBatch(toMigrate, installRoot, noProgress);
        }
    }

    private static void RunPrimaryInstall(
        List<ResolvedInstallRequest> requests, Action primaryAction, Task? predownloadTask)
    {
        SpectreAnsiConsole.MarkupLine("Setting up your environment.");
        if (requests.Count > 0)
        {
            DisplayInstallLocation(requests[0]);
        }

        // Wait for the predownload to finish (if still running) before starting the real install,
        // so the cache is populated and we avoid redundant downloads.
        predownloadTask?.GetAwaiter().GetResult();

        primaryAction();
    }

    private static PathPreference GetPathPreference(bool interactive, bool askEvenIfConfigured)
    {
        // If the user already configured their preference (e.g. prior walkthrough), reuse it.
        // In non-interactive mode, use the existing config or default to ShellProfile.
        PathPreference? existingPreference = DotnetupConfig.ReadPathPreference();
        if (existingPreference is not null && !askEvenIfConfigured)
        {
            return existingPreference.Value;
        }
        else if (!interactive)
        {
            if (!OperatingSystem.IsWindows() && ShellDetection.GetCurrentShellProvider() is null)
            {
                return PathPreference.DotnetupDotnet;
            }

            return PathPreference.ShellProfile;
        }

        if (!OperatingSystem.IsWindows() && ShellDetection.GetCurrentShellProvider() is null)
        {
            var shellEnv = Environment.GetEnvironmentVariable("SHELL") ?? "(not set)";
            SpectreAnsiConsole.MarkupLine(DotnetupTheme.Dim(
                $"[{DotnetupTheme.Current.Warning}]Warning:[/] Shell '{shellEnv.EscapeMarkup()}' is not supported for automatic environment configuration. dotnetup will continue without changing your shell profile."));
            return PathPreference.DotnetupDotnet;
        }

        var preference = PromptPathPreference();
        if (preference == PathPreference.FullPathReplacement && !OperatingSystem.IsWindows())
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.PlatformNotSupported,
                Strings.PathReplacementModeUnixError);
        }

        return preference;
    }

    // ── Prompt Functions ──

    /// <summary>
    /// Explains how dotnetup channels work and lets the user pick a channel.
    /// Builds example channels dynamically from the release manifest and shows
    /// what each one currently resolves to.
    /// </summary>
    private string PromptChannel()
    {
        string brand = DotnetupTheme.Current.Brand;
        string dim = DotnetupTheme.Current.Dim;

        SpectreAnsiConsole.MarkupLine($"Welcome to [{brand} bold]dotnetup[/]!");
        SpectreAnsiConsole.WriteLine();

        SpectreAnsiConsole.MarkupLine(string.Format(
            CultureInfo.InvariantCulture,
            "dotnetup updates and groups installations using [{0} bold]dotnetup channels[/].",
            brand));

        var globalJsonInfo = GlobalJsonModifier.GetGlobalJsonInfo(Environment.CurrentDirectory);
        if (globalJsonInfo.GlobalJsonPath is not null)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(
                CultureInfo.InvariantCulture,
                "[{0}]Channels may be implied from your global.json at [{1}]{2}[/].[/]",
                dim,
                brand,
                globalJsonInfo.GlobalJsonPath.EscapeMarkup()));
        }

        var examples = BuildChannelExamples();

        var prompt = new SelectionPrompt<ChannelExample>()
            .Title(string.Format(CultureInfo.InvariantCulture, "[bold]Select an example channel to get started:[/] [{0}](Enter to confirm)[/]", dim))
            .PageSize(5)
            .HighlightStyle(Style.Parse(brand))
            .MoreChoicesText(string.Format(CultureInfo.InvariantCulture, "[{0}](use {1}{2} arrows)[/]", dim, Constants.Symbols.UpArrow, Constants.Symbols.DownArrow))
            .UseConverter(ex => FormatChannelExample(ex, brand, dim));

        prompt.AddChoices(examples);

        if (Console.IsInputRedirected)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(
                CultureInfo.InvariantCulture,
                "[{0}]Using default channel: [{1}]latest[/][/]",
                dim, brand));
            return ChannelVersionResolver.LatestChannel;
        }

        var selected = SpectreAnsiConsole.Prompt(prompt);
        SpectreAnsiConsole.WriteLine();
        return selected.Channel;
    }

    /// <summary>
    /// Prompts the user to choose how they want to access the dotnetup-managed dotnet
    /// using an interactive selector that shows all options with descriptions and tooltips.
    /// </summary>
    internal static PathPreference PromptPathPreference()
    {
        bool isWindows = OperatingSystem.IsWindows();

        string isolationTooltip = string.Format(
            CultureInfo.InvariantCulture,
            Strings.PathTooltipDotnetupDotnet,
            isWindows ? "Program Files" : "/usr/local");

        string terminalTooltip = isWindows
            ? Strings.PathTooltipShellProfile + " " + Strings.PathTooltipShellProfileWindowsNote
            : Strings.PathTooltipShellProfile;

        var options = new List<SelectableOption>
        {
            new("i", Strings.PathPreferenceDotnetupDotnet, Strings.PathDescriptionDotnetupDotnet, isolationTooltip),
            new("t", Strings.PathPreferenceShellProfile,   isWindows ? Strings.PathDescriptionShellProfile : Strings.PathDescriptionShellProfileBase,   terminalTooltip),
        };

        if (isWindows)
        {
            options.Add(new("r", Strings.PathPreferenceFullReplacement, Strings.PathDescriptionFullReplacement, Strings.PathTooltipFullReplacement));
        }

        int selected = InteractiveOptionSelector.Show("How would you like to use dotnetup?", options, defaultIndex: 1);

        return selected switch
        {
            0 => PathPreference.DotnetupDotnet,
            1 => PathPreference.ShellProfile,
            _ => PathPreference.FullPathReplacement,
        };
    }

    /// <summary>
    /// Prompts the user about copying admin-managed installs into the dotnetup-managed directory.
    /// Installs already tracked in the dotnetup manifest for <paramref name="installRoot"/> are excluded.
    /// </summary>
    /// <returns>A list of installs to migrate if the user agrees, or an empty list if they decline or no unconverted system installs exist.</returns>
    internal static List<DotnetInstall> PromptInstallsToMigrateIfDesired(IDotnetEnvironmentManager dotnetEnvironment, PathPreference pathPreference, DotnetInstallRoot installRoot, string? manifestPath = null, bool askEvenIfConfigured = false)
    {
        if (!ShouldPromptToConvertSystemInstalls(pathPreference, ignoreConfig: askEvenIfConfigured))
        {
            return [];
        }

        var systemInstalls = dotnetEnvironment.GetExistingSystemInstalls();
        if (systemInstalls.Count == 0)
        {
            return [];
        }

        systemInstalls = FilterAlreadyTrackedInstalls(systemInstalls, installRoot, manifestPath);
        if (systemInstalls.Count == 0)
        {
            return [];
        }

        return PromptUserForMigration(systemInstalls, dotnetEnvironment, askEvenIfConfigured);
    }

    /// <summary>
    /// Filters out system installs already tracked in the dotnetup manifest using
    /// channel-based matching (e.g. runtime 10.0.4 is skipped if 10.0.5 is tracked).
    /// Prune stale entries first so manually-deleted installs don't persist.
    /// </summary>
    private static List<DotnetInstall> FilterAlreadyTrackedInstalls(
        List<DotnetInstall> systemInstalls, DotnetInstallRoot installRoot, string? manifestPath)
    {
        List<Installation> trackedInstalls;
        using (new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(manifestPath);
            var manifestData = manifest.ReadManifest();
            var root = manifestData.DotnetRoots.FirstOrDefault(r =>
                DotnetupUtilities.PathsEqual(Path.GetFullPath(r.Path), Path.GetFullPath(installRoot.Path)) && r.Architecture == installRoot.Architecture);
            trackedInstalls = root?.Installations ?? [];
        }

        return [.. systemInstalls
            .Where(sysInstall =>
            {
                string sysChannel = DotnetupUtilities.VersionToPatchBasedChannel(sysInstall.Version, sysInstall.Component);
                return !trackedInstalls.Exists(tracked =>
                {
                    if (tracked.Component != sysInstall.Component)
                    {
                        return false;
                    }

                    if (ReleaseVersion.TryParse(tracked.Version, out var trackedVersion))
                    {
                        return DotnetupUtilities.VersionToPatchBasedChannel(trackedVersion, tracked.Component) == sysChannel;
                    }

                    return tracked.Version == sysInstall.Version.ToString();
                });
            }),];
    }

    private static List<DotnetInstall> PromptUserForMigration(
        List<DotnetInstall> systemInstalls, IDotnetEnvironmentManager dotnetEnvironment, bool askEvenIfConfigured)
    {
        // Find the system install path for display purposes
        var currentInstall = dotnetEnvironment.GetCurrentPathConfiguration();
        string systemPath = currentInstall?.InstallType == InstallType.System
            ? currentInstall.Path
            : DotnetEnvironmentManager.GetSystemDotnetPaths().FirstOrDefault() ?? "the system .NET location";

        SpectreAnsiConsole.MarkupLine($"You have existing system install(s) of .NET in [{DotnetupTheme.Current.Accent}]{systemPath.EscapeMarkup()}[/].");

        var displayItems = systemInstalls
            .OrderBy(i => i.Component)
            .ThenByDescending(i => i.Version)
            .Select(i => string.Format(CultureInfo.InvariantCulture, "{0} {1}", i.Component.GetDisplayName(), i.Version))
            .ToList();

        var confirmResult = SpectreDisplayHelpers.RenderScrollableListWithConfirm(
            displayItems,
            visibleCount: 3,
            "Do you want to copy these installs into the dotnetup managed directory?",
            allowNeverAsk: true);

        HandleMigrationConfirmResult(confirmResult, askEvenIfConfigured);
        return confirmResult == ConfirmResult.Yes ? systemInstalls : [];
    }

    /// <summary>
    /// Persists the user's migration-prompt decision: clears a prior opt-out on accept,
    /// sets <see cref="DotnetupConfigData.DisableInstallConversion"/> on "never ask again",
    /// or shows a hint on decline.
    /// </summary>
    private static void HandleMigrationConfirmResult(ConfirmResult confirmResult, bool askEvenIfConfigured)
    {
        if (confirmResult == ConfirmResult.Yes)
        {
            if (askEvenIfConfigured)
            {
                var config = DotnetupConfig.Read() ?? new DotnetupConfigData();
                if (config.DisableInstallConversion)
                {
                    config.DisableInstallConversion = false;
                    DotnetupConfig.Write(config);
                }
            }

            SpectreAnsiConsole.MarkupLine($"[{DotnetupTheme.Current.Dim}]These will be installed after your setup completes.[/]");
        }
        else if (confirmResult == ConfirmResult.NeverAskAgain)
        {
            var config = DotnetupConfig.Read() ?? new DotnetupConfigData();
            config.DisableInstallConversion = true;
            DotnetupConfig.Write(config);
        }
        else
        {
            SpectreAnsiConsole.MarkupLine($"[{DotnetupTheme.Current.Dim}]You can run the walkthrough again to reconfigure.[/]");
        }
    }

    // ── Migration Batch ──

    /// <summary>
    /// Installs migrated system installs in two phases: SDKs first (their archives
    /// typically bundle runtimes), then runtimes that aren't already on disk.
    /// Failures are collected from both phases and reported at the end.
    /// </summary>
    private static void ExecuteMigrationBatch(List<DotnetInstall> toMigrate, DotnetInstallRoot installRoot, bool noProgress)
    {
        var sdks = toMigrate.Where(i => i.Component == InstallComponent.SDK).ToList();
        var runtimes = toMigrate.Where(i => i.Component != InstallComponent.SDK).ToList();
        var allFailures = new List<InstallFailure>();

        // Phase 1: Install SDKs first — their archives typically bundle runtime binaries.
        if (sdks.Count > 0)
        {
            var sdkRequests = sdks.Select(i => new ResolvedInstallRequest(
                new DotnetInstallRequest(
                    installRoot,
                    new UpdateChannel(DotnetupUtilities.VersionToPatchBasedChannel(i.Version, i.Component)),
                    i.Component,
                    new InstallRequestOptions()),
                i.Version)).ToList();

            var sdkResult = InstallExecutor.ExecuteInstalls(sdkRequests, noProgress);
            allFailures.AddRange(sdkResult.Failures);
        }

        SpectreAnsiConsole.WriteLine();

        // Phase 2: Skip runtimes whose folders already landed on disk via SDK archives.
        var remainingRuntimes = runtimes.Where(r => !RuntimeFolderExistsOnDisk(installRoot, r)).ToList();

        if (remainingRuntimes.Count > 0)
        {
            var runtimeRequests = remainingRuntimes.Select(i => new ResolvedInstallRequest(
                new DotnetInstallRequest(
                    installRoot,
                    new UpdateChannel(DotnetupUtilities.VersionToPatchBasedChannel(i.Version, i.Component)),
                    i.Component,
                    new InstallRequestOptions()),
                i.Version)).ToList();

            var runtimeResult = InstallExecutor.ExecuteInstalls(runtimeRequests, noProgress);
            allFailures.AddRange(runtimeResult.Failures);
        }

        if (allFailures.Count > 0)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(
                CultureInfo.InvariantCulture,
                "\n[{0}]{1} of {2} migration install(s) failed. " +
                "You can retry them later with \"dotnetup install\".[/]",
                DotnetupTheme.Current.Warning,
                allFailures.Count,
                toMigrate.Count));
        }
    }

    /// <summary>
    /// Checks whether a runtime's framework folder already exists on disk,
    /// typically because it was bundled inside an SDK archive.
    /// </summary>
    private static bool RuntimeFolderExistsOnDisk(DotnetInstallRoot installRoot, DotnetInstall runtime)
    {
        string frameworkDir = Path.Combine(
            installRoot.Path,
            "shared",
            runtime.Component.GetFrameworkName(),
            runtime.Version.ToString());
        return Directory.Exists(frameworkDir);
    }

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

    private static void ShowBanner()
    {
        SpectreAnsiConsole.Write(DotnetBotBanner.BuildPanel());
        SpectreAnsiConsole.WriteLine();
    }

    /// <summary>
    /// Builds a list of example channels with descriptions and resolved versions.
    /// Uses the release manifest to find the latest major version dynamically.
    /// </summary>
    private List<ChannelExample> BuildChannelExamples()
    {
        var resolver = _channelVersionResolver;

        var latestResolved = resolver.GetLatestVersionForChannel(
            new UpdateChannel(ChannelVersionResolver.LatestChannel), InstallComponent.SDK);
        string? ltsVersion = resolver.GetLatestVersionForChannel(
            new UpdateChannel(ChannelVersionResolver.LtsChannel), InstallComponent.SDK)?.ToString();
        string? previewVersion = resolver.GetLatestVersionForChannel(
            new UpdateChannel(ChannelVersionResolver.PreviewChannel), InstallComponent.SDK)?.ToString();

        var examples = new List<ChannelExample>
        {
            new(ChannelVersionResolver.LatestChannel, "Latest stable release", latestResolved?.ToString()),
            new(NoneChannel, "I'll tell you what to install later.", null),
            new(ChannelVersionResolver.LtsChannel, "Long Term Support", ltsVersion),
            new(ChannelVersionResolver.PreviewChannel, "Latest preview", previewVersion),
        };

        if (latestResolved is not null)
        {
            string latestVersion = latestResolved.ToString();
            string majorMinor = FormattableString.Invariant($"{latestResolved.Major}.{latestResolved.Minor}");
            string featureBand = FormattableString.Invariant($"{latestResolved.Major}.{latestResolved.Minor}.{latestResolved.SdkFeatureBand / 100}xx");

            examples.Add(new(majorMinor, "Major.Minor channel", latestVersion));
            examples.Add(new(featureBand, "SDK feature band", latestVersion));
            examples.Add(new(latestVersion, "Explicit version", latestVersion));
        }

        return examples;
    }

    private static string FormatChannelExample(ChannelExample ex, string brand, string dim)
    {
        if (ex.Channel == NoneChannel)
        {
            return string.Format(CultureInfo.InvariantCulture, "[bold {0}]{1}[/]  [{2}]{3}[/]",
                brand,
                ex.Channel.EscapeMarkup().PadRight(12),
                dim,
                ex.Description.EscapeMarkup());
        }

        string resolved = ex.ResolvedVersion is not null
            ? string.Format(CultureInfo.InvariantCulture, "[{0}] {1} {2}[/]", dim, Constants.Symbols.RightArrow, ex.ResolvedVersion)
            : string.Format(CultureInfo.InvariantCulture, "[{0}] (no version available)[/]", dim);
        string suggested = ex.Channel == ChannelVersionResolver.LatestChannel
            ? " [white](suggested)[/]"
            : "";
        return string.Format(CultureInfo.InvariantCulture, "[bold {0}]{1}[/]{2}  [{3}]{4}[/] {5}",
            brand,
            ex.Channel.EscapeMarkup().PadRight(12),
            suggested,
            dim,
            ex.Description.EscapeMarkup(),
            resolved);
    }

    private static void SaveConfigAndDisplayResult(PathPreference pathPreference, PathPreference? previousPreference)
    {
        var config = new DotnetupConfigData
        {
            PathPreference = pathPreference,
        };

        DotnetupConfig.Write(config);

        // Only show guidance when the preference actually changed (or first-time setup).
        if (previousPreference != pathPreference)
        {
            DisplayPathGuidance(pathPreference);
        }

        SpectreAnsiConsole.MarkupLine(DotnetupTheme.Brand("Setup complete!"));
    }

    /// <summary>
    /// Shows guidance based on the chosen path preference.
    /// </summary>
    private static void DisplayPathGuidance(PathPreference preference)
    {
        string? guidance = preference switch
        {
            PathPreference.DotnetupDotnet => Strings.PathGuidanceDotnetupDotnet,
            PathPreference.ShellProfile => Strings.PathGuidanceShellProfile,
            PathPreference.FullPathReplacement => Strings.PathGuidanceFullReplacement,
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
