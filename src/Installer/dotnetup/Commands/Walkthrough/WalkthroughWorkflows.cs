// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Globalization;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Spectre.Console;
using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Walkthrough;

/// <summary>
/// Runs the interactive walkthrough that installs the .NET SDK with defaults
/// and records the user's path replacement preference to <c>dotnetup.config.json</c>.
/// </summary>
internal class WalkthroughWorkflows()

{

    public FullIntroductionWalkthrough()
    {
        ShowBanner();

        // Step 0: Explain channels and let the user pick one
        string selectedChannel = PromptChannel();

        var (channel, globalJson, installRoot) = ResolveChannelAndStartPredownload(selectedChannel);

        BaseConfigurationWalkthrough();
    }

    public BaseConfigurationWalkthrough()
    {
        // Step 1: Choose how to access .NET
        var pathPreference = PromptPathPreference();

        if (pathPreference == PathPreference.FullPathReplacement && !OperatingSystem.IsWindows())
        {
            SpectreAnsiConsole.MarkupLine(DotnetupTheme.Error(Strings.PathReplacementModeUnixError));
            return 1;
        }

        // Both FullPathReplacement and ShellProfile shadow the system PATH, so
        // dotnetup needs to be the default install for both modes.
        // DotnetupDotnet (isolation) doesn't touch PATH at all.
        bool? replaceSystemConfig = InstallWalkthrough.ShouldReplaceSystemConfiguration(pathPreference);

        // Step 2: Prompt about admin installs before setting up the environment.
        // Both ShellProfile and FullPathRe lacement shadow admin installs, so offer migration for both.
        // Track the migration decision separately — accepting migration should copy installs
        // but only FullPathReplacement should trigger system PATH changes (elevation).
        List<DotnetInstall> toMigrate = InstallWalkthrough.GetInstallsToMigrateIfDesired(_dotnetInstaller, pathPreference);

        SpectreAnsiConsole.MarkupLine("Setting up your environment.");
        ValidateInstallPathOrThrow(installRoot, _manifestPath);
        DisplayInstallLocation(globalJson);


        // Step 2: Run the install workflow, which will download and set up the SDK

        // Step 3: Save config — show guidance and "Setup complete!" before migrating admin installs
        SaveConfigAndDisplayResult(pathPreference);


        SpectreAnsiConsole.MarkupLine(DotnetupTheme.Dim(
            "You may now use dotnetup. In the meantime, we'll install your remaining components."));
        // make sure we get rid of the one we already installed first 
        InstallExecutor.ExecuteAdditionalInstalls(
            toMigrate,
            workflowResult.InstallRoot,
            workflowResult.ManifestPath,
            workflowResult.NoProgress,
            workflowResult.RequireMuxerUpdate);
    }
}
