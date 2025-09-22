﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Graph;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal sealed class BrowserLauncher(ILogger logger, EnvironmentOptions environmentOptions)
{
    // interlocked
    private ImmutableHashSet<ProjectInstanceId> _browserLaunchAttempted = [];

    /// <summary>
    /// Installs browser launch/reload trigger.
    /// </summary>
    public void InstallBrowserLaunchTrigger(
        ProcessSpec processSpec,
        ProjectGraphNode projectNode,
        ProjectOptions projectOptions,
        BrowserRefreshServer? server,
        CancellationToken cancellationToken)
    {
        if (!CanLaunchBrowser(projectOptions, out var launchProfile))
        {
            if (environmentOptions.TestFlags.HasFlag(TestFlags.MockBrowser))
            {
                logger.LogError("Test requires browser to launch");
            }

            return;
        }

        WebServerProcessStateObserver.Observe(projectNode, processSpec, url =>
        {
            if (projectOptions.IsRootProject &&
                ImmutableInterlocked.Update(ref _browserLaunchAttempted, static (set, key) => set.Add(key), projectNode.GetProjectInstanceId()))
            {
                // first build iteration of a root project:
                var launchUrl = GetLaunchUrl(launchProfile.LaunchUrl, url);
                LaunchBrowser(launchUrl, server);
            }
            else if (server != null)
            {
                // Subsequent iterations (project has been rebuilt and relaunched).
                // Use refresh server to reload the browser, if available.
                _ = server.SendReloadMessageAsync(cancellationToken).AsTask();
            }
        });
    }

    public static string GetLaunchUrl(string? profileLaunchUrl, string outputLaunchUrl)
        => string.IsNullOrWhiteSpace(profileLaunchUrl) ? outputLaunchUrl :
            Uri.TryCreate(profileLaunchUrl, UriKind.Absolute, out _) ? profileLaunchUrl :
            Uri.TryCreate(outputLaunchUrl, UriKind.Absolute, out var launchUri) ? new Uri(launchUri, profileLaunchUrl).ToString() :
            outputLaunchUrl;

    private void LaunchBrowser(string launchUrl, BrowserRefreshServer? server)
    {
        var fileName = launchUrl;

        var args = string.Empty;
        if (environmentOptions.BrowserPath is { } browserPath)
        {
            args = fileName;
            fileName = browserPath;
        }

        logger.LogDebug("Launching browser: {FileName} {Args}", fileName, args);

        if (environmentOptions.TestFlags != TestFlags.None)
        {
            if (environmentOptions.TestFlags.HasFlag(TestFlags.MockBrowser))
            {
                Debug.Assert(server != null);
                server.EmulateClientConnected();
            }

            return;
        }

        var info = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            UseShellExecute = true,
        };

        try
        {
            using var browserProcess = Process.Start(info);
            if (browserProcess is null or { HasExited: true })
            {
                // dotnet-watch, by default, relies on URL file association to launch browsers. On Windows and MacOS, this works fairly well
                // where URLs are associated with the default browser. On Linux, this is a bit murky.
                // From emperical observation, it's noted that failing to launch a browser results in either Process.Start returning a null-value
                // or for the process to have immediately exited.
                // We can use this to provide a helpful message.
                logger.LogInformation("Unable to launch the browser. Url '{Url}'.", launchUrl);
            }
        }
        catch (Exception e)
        {
            logger.LogDebug("Failed to launch a browser: {Message}", e.Message);
        }
    }

    private bool CanLaunchBrowser(ProjectOptions projectOptions, [NotNullWhen(true)] out LaunchSettingsProfile? launchProfile)
    {
        launchProfile = null;

        if (environmentOptions.SuppressLaunchBrowser)
        {
            return false;
        }

        if (!CommandLineOptions.IsCodeExecutionCommand(projectOptions.Command))
        {
            logger.LogDebug("Command '{Command}' does not support launching browsers.", projectOptions.Command);
            return false;
        }

        launchProfile = GetLaunchProfile(projectOptions);
        if (launchProfile is not { LaunchBrowser: true })
        {
            logger.LogDebug("launchSettings does not allow launching browsers.");
            return false;
        }

        logger.Log(MessageDescriptor.ConfiguredToLaunchBrowser);
        return true;
    }

    private LaunchSettingsProfile GetLaunchProfile(ProjectOptions projectOptions)
    {
        return (projectOptions.NoLaunchProfile == true
            ? null : LaunchSettingsProfile.ReadLaunchProfile(projectOptions.ProjectPath, projectOptions.LaunchProfileName, logger)) ?? new();
    }
}
