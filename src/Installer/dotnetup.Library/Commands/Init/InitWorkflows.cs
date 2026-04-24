// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Spectre.Console;
using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;

/// <summary>
/// Orchestrates the interactive init/onboarding flow that configures the user's
/// environment and records the path replacement preference to
/// <c>dotnetup.config.json</c>.
/// </summary>
internal class InitWorkflows
{
    private readonly IDotnetEnvironmentManager _dotnetEnvironment;
    private readonly ChannelVersionResolver _channelVersionResolver;

    /// <summary>Sentinel channel value indicating the user wants to skip the initial install.</summary>
    internal const string NoneChannel = "none";

    private sealed record ChannelExample(string Channel, string Description, string? ResolvedVersion);
    internal sealed record MigrationSelection(
        InstallComponent Component,
        UpdateChannel Channel,
        ReleaseVersion ExampleVersion,
        InstallArchitecture Architecture);

    public InitWorkflows(IDotnetEnvironmentManager dotnetEnvironment, ChannelVersionResolver channelVersionResolver)
    {
        _dotnetEnvironment = dotnetEnvironment;
        _channelVersionResolver = channelVersionResolver;
    }

    /// <summary>
    /// Returns true when the given <see cref="PathPreference"/> implies we should
    /// replace the default dotnet installation (i.e. update PATH / DOTNET_ROOT).
    /// </summary>
    public static bool ShouldReplaceSystemConfiguration(PathPreference preference) =>
        preference is PathPreference.FullPathReplacement;

    /// <summary>
    /// Returns true when the user chose a mode that shadows the system PATH and should therefore
    /// be offered migration of existing system-level .NET installs into dotnetup-managed installs.
    /// </summary>
    public static bool ShouldPromptToConvertSystemInstalls(PathPreference preference)
    {
        return preference != PathPreference.DotnetupDotnet;
    }

    // ── Init Flow Orchestrators ──

    /// <summary>
    /// Interactive onboarding flow used both by the explicit <c>dotnetup init</c> command
    /// and by the first interactive install when dotnetup has not yet been configured.
    /// When <paramref name="requests"/> is not supplied, the user is asked to pick a starter
    /// channel. When it is supplied, the walkthrough reuses the already-resolved install
    /// requests and skips the extra channel prompt.
    /// </summary>
    public List<ResolvedInstallRequest> InitWalkthrough(
        InstallCommand command,
        List<ResolvedInstallRequest>? requests = null)
    {
        ShowBanner();
        var effectiveRequests = ResolveWalkthroughRequests(command, requests);

        // Determine the install root for environment configuration and migration.
        // Use the first request's root if available, otherwise fall back to the default path.
        DotnetInstallRoot installRoot = effectiveRequests.Count > 0
            ? effectiveRequests[0].Request.InstallRoot
            : new DotnetInstallRoot(
                _dotnetEnvironment.GetDefaultDotnetInstallPath(),
                InstallerUtilities.GetDefaultInstallArchitecture());

        // Fire off background predownload while the user answers prompts.
        Task? predownloadTask = effectiveRequests.Count > 0
            ? InstallerOrchestratorSingleton.PredownloadToCacheAsync(effectiveRequests[0])
            : null;

        // User chooses how to access .NET
        PathPreference? previousPreference = DotnetupConfig.ReadPathPreference();
        PathPreference pathPreference = GetInitPathPreference(command.Interactive, command.ShellProvider);
        string? manifestPath = effectiveRequests.Count > 0 ? effectiveRequests[0].Request.Options.ManifestPath : null;

        // Step 2: Prompt about admin installs before setting up the environment.
        List<MigrationSelection> toMigrate = PromptInstallsToMigrateIfDesired(
            _dotnetEnvironment,
            pathPreference,
            installRoot,
            manifestPath,
            effectiveRequests,
            command.Interactive);

        if (toMigrate.Count > 0)
        {
            effectiveRequests = MergeInstallRequests(effectiveRequests, toMigrate, installRoot, manifestPath);
        }

        // Step 3: Run the primary install (typically the base SDK from global.json/latest)
        // and any selected migration installs before completing setup.
        RunInstallRequests(effectiveRequests, predownloadTask, command.NoProgress);

        // Save config and apply configuration(s).
        SaveConfigAndDisplayResult(pathPreference, previousPreference);

        if (pathPreference is PathPreference.ShellProfile)
        {
            _dotnetEnvironment.ApplyTerminalProfileModifications(installRoot.Path, command.ShellProvider);
        }

        if (ShouldReplaceSystemConfiguration(pathPreference))
        {
            _dotnetEnvironment.ApplyEnvironmentModifications(InstallType.User, installRoot.Path);
        }

        return effectiveRequests;
    }

    private List<ResolvedInstallRequest> ResolveWalkthroughRequests(
        InstallCommand command,
        List<ResolvedInstallRequest>? requests)
    {
        if (requests is not null)
        {
            return requests;
        }

        // Step 0: Explain channels and let the user pick one
        string selectedChannel = PromptChannel();
        if (selectedChannel == NoneChannel)
        {
            return [];
        }

        // Generate the install request via the workflow (handles path resolution, global.json, validation)
        var workflow = new InstallWorkflow(command);
        return workflow.GenerateInstallRequests(
            [new MinimalInstallSpec(InstallComponent.SDK, selectedChannel)]);
    }

    private static void RunInstallRequests(
        List<ResolvedInstallRequest> requests,
        Task? predownloadTask,
        bool noProgress)
    {
        SpectreAnsiConsole.MarkupLine("Setting up your environment.");
        if (requests.Count > 0)
        {
            DisplayInstallLocation(requests[0]);
        }

        // Wait for the predownload to finish (if still running) before starting the real install,
        // so the cache is populated and we avoid redundant downloads.
        predownloadTask?.GetAwaiter().GetResult();

        InstallExecutor.ExecuteInstalls(requests, noProgress);
    }

    private static PathPreference GetInitPathPreference(bool interactive, IEnvShellProvider? shellProvider = null)
    {
        if (!interactive)
        {
            if (!OperatingSystem.IsWindows() && (shellProvider ?? ShellDetection.GetCurrentShellProvider()) is null)
            {
                return PathPreference.DotnetupDotnet;
            }

            return PathPreference.ShellProfile;
        }

        if (!OperatingSystem.IsWindows() && (shellProvider ?? ShellDetection.GetCurrentShellProvider()) is null)
        {
            SpectreAnsiConsole.MarkupLine(DotnetupTheme.Dim(
                $"[{DotnetupTheme.Current.Warning}]Warning:[/] Shell '{ShellDetection.GetCurrentShellDisplayName().EscapeMarkup()}' is not supported for automatic environment configuration. dotnetup will continue without changing your shell profile unless you specify one with --shell."));
            return PathPreference.DotnetupDotnet;
        }

        return ValidatePathPreference(PromptPathPreference());
    }

    private static PathPreference ValidatePathPreference(PathPreference preference)
    {
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
    /// Prompts the user about migrating system installs into the dotnetup-managed directory.
    /// Existing installs are normalized to update channels and deduplicated before prompting.
    /// </summary>
    /// <returns>A list of deduplicated channel selections to migrate, or an empty list if the user declines or no candidates remain.</returns>
    internal static List<MigrationSelection> PromptInstallsToMigrateIfDesired(
        IDotnetEnvironmentManager dotnetEnvironment,
        PathPreference pathPreference,
        DotnetInstallRoot installRoot,
        string? manifestPath = null,
        IReadOnlyCollection<ResolvedInstallRequest>? existingRequests = null,
        bool interactive = true)
    {
        if (!ShouldPromptToConvertSystemInstalls(pathPreference))
        {
            return [];
        }

        if (!interactive)
        {
            return [];
        }

        var systemInstalls = GetMigrationCandidates(dotnetEnvironment);
        var migrationSelections = BuildMigrationSelections(systemInstalls, installRoot, manifestPath, existingRequests);
        if (migrationSelections.Count == 0)
        {
            return [];
        }

        return PromptUserForMigration(migrationSelections, dotnetEnvironment);
    }

    internal static List<DotnetInstall> GetMigrationCandidates(
        IDotnetEnvironmentManager dotnetEnvironment,
        IReadOnlyCollection<InstallComponent>? components = null)
    {
        var systemInstalls = dotnetEnvironment.GetExistingSystemInstalls();
        if (systemInstalls.Count == 0)
        {
            return [];
        }

        if (components is { Count: > 0 })
        {
            systemInstalls = [.. systemInstalls.Where(i => components.Contains(i.Component))];
        }
        return systemInstalls;
    }

    /// <summary>
    /// Returns the install-spec channels already represented in the manifest or the
    /// current request set for the target install root.
    /// </summary>
    private static HashSet<(InstallComponent Component, string Channel)> GetTrackedMigrationChannels(
        DotnetInstallRoot installRoot,
        string? manifestPath,
        IReadOnlyCollection<ResolvedInstallRequest>? existingRequests = null)
    {
        var trackedChannels = new HashSet<(InstallComponent Component, string Channel)>();

        using (new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(manifestPath);
            var manifestData = manifest.ReadManifest();
            var root = manifestData.DotnetRoots.FirstOrDefault(r =>
                DotnetupUtilities.PathsEqual(Path.GetFullPath(r.Path), Path.GetFullPath(installRoot.Path)) && r.Architecture == installRoot.Architecture);

            foreach (var installSpec in root?.InstallSpecs ?? [])
            {
                trackedChannels.Add((installSpec.Component, GetTrackedMigrationChannelName(installSpec.Component, installSpec.VersionOrChannel)));
            }
        }

        if (existingRequests is not null)
        {
            foreach (var request in existingRequests)
            {
                trackedChannels.Add((request.Request.Component, GetTrackedMigrationChannelName(request.Request.Component, request.Request.Channel.Name)));
            }
        }

        return trackedChannels;
    }

    internal static List<MigrationSelection> BuildMigrationSelections(
        List<DotnetInstall> systemInstalls,
        DotnetInstallRoot installRoot,
        string? manifestPath = null,
        IReadOnlyCollection<ResolvedInstallRequest>? existingRequests = null)
    {
        if (systemInstalls.Count == 0)
        {
            return [];
        }

        var trackedChannels = GetTrackedMigrationChannels(installRoot, manifestPath, existingRequests);
        var deduped = new List<MigrationSelection>();
        var seenChannels = new HashSet<(InstallComponent Component, string Channel)>();

        foreach (var install in systemInstalls.OrderBy(i => i.Component).ThenByDescending(i => i.Version))
        {
            string channelName = DotnetupUtilities.VersionToPatchBasedChannel(install.Version, install.Component);
            var key = (install.Component, GetTrackedMigrationChannelName(install.Component, channelName));
            if (trackedChannels.Contains(key))
            {
                continue;
            }

            if (!seenChannels.Add(key))
            {
                continue;
            }

            deduped.Add(new MigrationSelection(
                install.Component,
                new UpdateChannel(channelName),
                install.Version,
                install.InstallRoot.Architecture));
        }

        return deduped;
    }

    private static string GetTrackedMigrationChannelName(InstallComponent component, string channelName)
    {
        if (component != InstallComponent.SDK &&
            int.TryParse(channelName, out int major))
        {
            return $"{major}.0";
        }

        return channelName;
    }

    internal static List<string> FormatMigrationDisplayItems(List<MigrationSelection> migrationSelections)
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

    internal static List<MigrationSelection> PromptUserForMigration(
        List<MigrationSelection> migrationSelections,
        IDotnetEnvironmentManager dotnetEnvironment)
    {
        if (Console.IsInputRedirected)
        {
            SpectreAnsiConsole.MarkupLine(
                $"[{DotnetupTheme.Current.Dim}]Skipping the migration prompt because interactive input is not available. {GetMigrationRetryHint().EscapeMarkup()}[/]");
            return [];
        }

        // Find the system install path for display purposes
        var currentInstall = dotnetEnvironment.GetCurrentPathConfiguration();
        string systemPath = currentInstall?.InstallType == InstallType.System
            ? currentInstall.Path
            : DotnetEnvironmentManager.GetSystemDotnetPaths().FirstOrDefault() ?? "the system .NET location";

        SpectreAnsiConsole.MarkupLine($"You have existing system-managed .NET installs in [{DotnetupTheme.Current.Accent}]{systemPath.EscapeMarkup()}[/].");

        var displayItems = FormatMigrationDisplayItems(migrationSelections);

        var confirmResult = SpectreDisplayHelpers.RenderScrollableListWithConfirm(
            displayItems,
            visibleCount: 3,
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

    internal static List<ResolvedInstallRequest> MergeInstallRequests(
        List<ResolvedInstallRequest> requests,
        List<MigrationSelection> toMigrate,
        DotnetInstallRoot installRoot,
        string? manifestPath = null)
    {
        if (toMigrate.Count == 0)
        {
            return requests;
        }

        var mergedRequests = new List<ResolvedInstallRequest>(requests);
        var existingRequests = requests
            .Select(r => (r.Request.Component, Channel: GetTrackedMigrationChannelName(r.Request.Component, r.Request.Channel.Name)))
            .ToHashSet();

        foreach (var migration in toMigrate.OrderBy(i => i.Component).ThenBy(i => i.Channel.Name))
        {
            var requestKey = (migration.Component, Channel: GetTrackedMigrationChannelName(migration.Component, migration.Channel.Name));
            if (!existingRequests.Add(requestKey))
            {
                continue;
            }

            mergedRequests.Add(new ResolvedInstallRequest(
                new DotnetInstallRequest(
                    installRoot,
                    migration.Channel,
                    migration.Component,
                    new InstallRequestOptions { ManifestPath = manifestPath }),
                migration.ExampleVersion));
        }

        return mergedRequests;
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
