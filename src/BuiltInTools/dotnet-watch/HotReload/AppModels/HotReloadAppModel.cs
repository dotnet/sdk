// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Graph;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal abstract partial class HotReloadAppModel(ProjectGraphNode? agentInjectionProject)
{
    public abstract bool RequiresBrowserRefresh { get; }

    public abstract HotReloadClients CreateClients(BrowserRefreshServer? browserRefreshServer, ILogger clientLogger, ILogger agentLogger);

    /// <summary>
    /// Returns true and the path to the client agent implementation binary if the application needs the agent to be injected.
    /// </summary>
    public bool TryGetStartupHookPath([NotNullWhen(true)] out string? path)
    {
        if (agentInjectionProject == null)
        {
            path = null;
            return false;
        }

        var hookTargetFramework = agentInjectionProject.GetTargetFramework() switch
        {
            // Note: Hot Reload is only supported on net6.0+
            "net6.0" or "net7.0" or "net8.0" or "net9.0" => "net6.0",
            _ => "net10.0",
        };

        path = Path.Combine(Path.GetDirectoryName(typeof(HotReloadAppModel).Assembly.Location)!, "hotreload", hookTargetFramework, "Microsoft.Extensions.DotNetDeltaApplier.dll");
        return true;
    }

    public static HotReloadAppModel InferFromProject(ProjectGraphNode projectNode, ILogger logger, EnvironmentOptions environmentOptions)
    {
        var capabilities = projectNode.GetCapabilities();

        if (capabilities.Contains(ProjectCapability.WebAssembly))
        {
            logger.Log(MessageDescriptor.HotReloadProfile_BlazorWebAssembly);
            return new BlazorWebAssemblyAppModel(clientProject: projectNode, environmentOptions);
        }

        if (capabilities.Contains(ProjectCapability.AspNetCore))
        {
            if (projectNode.GetDescendantsAndSelf().FirstOrDefault(static p => p.GetCapabilities().Contains(ProjectCapability.WebAssembly)) is { } clientProject)
            {
                logger.Log(MessageDescriptor.HotReloadProfile_BlazorHosted, projectNode.ProjectInstance.FullPath, clientProject.ProjectInstance.FullPath);
                return new BlazorWebAssemblyHostedAppModel(clientProject: clientProject, serverProject: projectNode, environmentOptions);
            }

            logger.Log(MessageDescriptor.HotReloadProfile_WebApplication);
            return new WebServerAppModel(serverProject: projectNode);
        }

        logger.Log(MessageDescriptor.HotReloadProfile_Default);
        return new DefaultAppModel(projectNode);
    }
}
