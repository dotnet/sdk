// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Graph;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal abstract partial class HotReloadAppModel()
{
    public abstract ValueTask<HotReloadClients> CreateClientsAsync(ILogger clientLogger, ILogger agentLogger, CancellationToken cancellationToken);

    protected static string GetInjectedAssemblyPath(string targetFramework, string assemblyName)
        => Path.Combine(Path.GetDirectoryName(typeof(HotReloadAppModel).Assembly.Location)!, "hotreload", targetFramework, assemblyName + ".dll");

    public static string GetStartupHookPath(ProjectGraphNode project)
    {
        var hookTargetFramework = project.GetTargetFrameworkVersion() is { Major: >= 10 } ? "net10.0" : "net6.0";
        return GetInjectedAssemblyPath(hookTargetFramework, "Microsoft.Extensions.DotNetDeltaApplier");
    }

    public static HotReloadAppModel InferFromProject(DotNetWatchContext context, ProjectGraphNode projectNode)
    {
        var capabilities = projectNode.GetCapabilities();

        if (capabilities.Contains(ProjectCapability.WebAssembly))
        {
            context.Logger.Log(MessageDescriptor.ApplicationKind_BlazorWebAssembly);
            return new BlazorWebAssemblyAppModel(context, clientProject: projectNode);
        }

        if (capabilities.Contains(ProjectCapability.AspNetCore))
        {
            if (projectNode.GetDescendantsAndSelf().FirstOrDefault(static p => p.GetCapabilities().Contains(ProjectCapability.WebAssembly)) is { } clientProject)
            {
                context.Logger.Log(MessageDescriptor.ApplicationKind_BlazorHosted, projectNode.ProjectInstance.FullPath, clientProject.ProjectInstance.FullPath);
                return new BlazorWebAssemblyHostedAppModel(context, clientProject: clientProject, serverProject: projectNode);
            }

            context.Logger.Log(MessageDescriptor.ApplicationKind_WebApplication);
            return new WebServerAppModel(context, serverProject: projectNode);
        }

        if (capabilities.Contains(ProjectCapability.HotReloadWebSockets))
        {
            context.Logger.Log(MessageDescriptor.ApplicationKind_WebSockets);
            return new MobileAppModel(context, projectNode);
        }

        context.Logger.Log(MessageDescriptor.ApplicationKind_Default);
        return new DefaultAppModel(projectNode);
    }

    /// <summary>
    /// True if a managed code agent can be injected into the target process.
    /// The agent is injected either via dotnet startup hook, or via web server middleware for WASM clients.
    /// </summary>
    internal static bool IsManagedAgentSupported(ProjectGraphNode project, ILogger logger)
    {
        if (!project.IsNetCoreApp(Versions.Version6_0))
        {
            logger.Log(MessageDescriptor.ProjectDoesNotSupportHotReload_TargetFramework, Versions.Version6_0);
            return false;
        }

        // Since .NET 11 the SDK generates runtimeconfig.dev.json file that configures the runtime to support
        // startup hooks and metadata update handlers in Debug builds.
        // The file is generated when the property EnableHotReloadInRuntimeConfigDevFile is true.
        if (project.IsNetCoreApp(Versions.Version11_0))
        {
            if (!project.ProjectInstance.GetBooleanPropertyValue(PropertyNames.EnableHotReloadInRuntimeConfigDevFile, defaultValue: true))
            {
                // If runtimeconfig.dev.json file is not generated MetadataUpdaterSupport and StartupHookSupport need to be enabled.
                var metadataUpdaterSupported = project.ProjectInstance.GetBooleanPropertyValue(PropertyNames.MetadataUpdaterSupport, defaultValue: true);
                if (!metadataUpdaterSupported || !StartupHookSupportedIfRequired())
                {
                    logger.Log(
                        MessageDescriptor.ProjectDoesNotSupportHotReload_Property,
                        // setting blocking Hot Reload:
                        metadataUpdaterSupported ? PropertyNames.StartupHookSupport : PropertyNames.MetadataUpdaterSupport,
                        "False",
                        // recommended setting:
                        PropertyNames.EnableHotReloadInRuntimeConfigDevFile,
                        "True");

                    return false;
                }
            }
        }
        else
        {
            // Startup hooks are not used for WASM projects.
            if (StartupHookSupportedIfRequired())
            {
                // Report which property is causing lack of support for startup hooks:
                var (propertyName, propertyValue) =
                    project.ProjectInstance.GetBooleanPropertyValue(PropertyNames.PublishAot)
                    ? (PropertyNames.PublishAot, true)
                    : project.ProjectInstance.GetBooleanPropertyValue(PropertyNames.PublishTrimmed)
                    ? (PropertyNames.PublishTrimmed, true)
                    : (PropertyNames.StartupHookSupport, false);

                logger.Log(MessageDescriptor.ProjectDoesNotSupportHotReload_Property, propertyName, propertyValue.ToString(), PropertyNames.StartupHookSupport, "True");
                return false;
            }

            // Not checking MetadataUpdateSupport since it's not set correctly prior .NET 11.
            // Use Optimize and DebugSymbols instead for older frameworks.
            // See https://github.com/dotnet/runtime/pull/127163

            if (project.ProjectInstance.GetBooleanPropertyValue(PropertyNames.Optimize))
            {
                logger.Log(MessageDescriptor.ProjectDoesNotSupportHotReload_Property, PropertyNames.Optimize, "True", PropertyNames.Optimize, "False");
                return false;
            }

            if (!project.ProjectInstance.GetBooleanPropertyValue(PropertyNames.DebugSymbols))
            {
                logger.Log(MessageDescriptor.ProjectDoesNotSupportHotReload_Property, PropertyNames.DebugSymbols, "False", PropertyNames.DebugSymbols, "True");
                return false;
            }
        }

        return true;

        bool StartupHookSupportedIfRequired()
            => project.ProjectInstance.GetBooleanPropertyValue(PropertyNames.StartupHookSupport, defaultValue: true) ||
               project.GetCapabilities().Contains(ProjectCapability.WebAssembly);
    }
}
